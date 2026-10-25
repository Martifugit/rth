using System.Collections.Generic;
using System.Linq;
using Rth.Shared.World;
using Rth.Shared.World.Ground;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Rth.Client
{
    /// <summary>
    /// Ground drawn tile by tile from a region's "tiles" piece (world/regions, docs/workflow.md),
    /// draped over the region's terrain (<see cref="corners"/>, from its data): one mesh with a
    /// submesh per kind, each kind lifted by its own amount (a rock shelf above, water below), sides
    /// where a tile stands above its neighbour, and a collider over the walkable tiles that aren't
    /// too steep, which is all the map exporter and <see cref="GroundHeight"/> count as ground.
    /// The scene keeps only the rows and heights; the meshes are rebuilt whenever it loads, so they
    /// cost the scene file nothing. The Editor's ground sculpting tool changes <see cref="corners"/>
    /// and marks it <see cref="sculpted"/>; its ground brush changes the rows and kinds and keeps them
    /// in <see cref="brushEdit"/>. Saving the scene writes both back to the data.
    /// </summary>
    [ExecuteAlways]
    public sealed class TileGround : MonoBehaviour
    {
        /// <summary>Every enabled tile ground, for height lookups (the camera's terrain lift).</summary>
        public static readonly List<TileGround> Active = new List<TileGround>();

        private static bool _showSurface = true;

        /// <summary>False while the Editor's terrain editor stands in for the ground (it draws the same heights).</summary>
        public static bool ShowSurface
        {
            get => _showSurface;
            set
            {
                _showSurface = value; // always applied: after a script reload the flag resets, the renderers don't
                foreach (TileGround g in Active)
                {
                    if (g != null && g._surface != null) g._surface.GetComponent<MeshRenderer>().enabled = value;
                    if (g != null && g._water != null) g._water.GetComponent<MeshRenderer>().enabled = value;
                }
            }
        }

        [Tooltip("Absolute tile of the south-west corner.")]
        public int originX, originY;
        public int width, height;
        [Tooltip("One string a row from the north, a character a tile from the west; a space is no ground.")]
        public string[] rows = new string[0];

        [Tooltip("The characters the rows use, one per kind.")]
        public string kinds = "";
        public Material[] materials = new Material[0];
        [Tooltip("How far each kind's top is drawn above (or below) the ground's height, in tiles.")]
        [FormerlySerializedAs("heights")] public float[] lifts = new float[0];
        [Tooltip("Kinds that are walkable ground (the rest block: rock, water).")]
        public bool[] walkable = new bool[0];

        [Tooltip("Corner heights in tiles, (width + 1) × (height + 1), row by row from the south. Empty: flat.")]
        [HideInInspector] public float[] corners = new float[0];
        [Tooltip("A tile rising more than this many tiles across is a cliff: drawn in the cliff material and blocked.")]
        public float cliffStep = 1f;
        [Tooltip("Drawn over cliff faces; none: their own kind's material.")]
        public Material cliffMaterial;
        [Tooltip("Per kind, its own cliff faces (none: the shared cliff material).")]
        public Material[] cliffMaterials = new Material[0];
        [Tooltip("Set by sculpting in the Editor: saving writes the heights back to the region's data.")]
        [HideInInspector] public bool sculpted;
        [Tooltip("Set by the Editor's ground brush: the ground's tiles, kinds, ring and colours as JSON; saving writes them to the region's data.")]
        [HideInInspector] public string brushEdit = "";

        [Header("Painted ground (docs/world-plan.md section 7)")]
        [Tooltip("Painted the RuneScape way: natural ground blended, made surfaces and water crisp with diagonal half tiles, in one texture.")]
        public bool painted;
        [Tooltip("Tiles of the neighbours' ground round the region in Surround, for blending across borders.")]
        public int margin;
        [Tooltip("The neighbours' tiles: full rows along the north and south, the west and east parts between (Piece.Surround).")]
        public string[] surround = new string[0];
        [Tooltip("Per kind: a made surface (road, paving, planks), drawn crisp.")]
        public bool[] overlay = new bool[0];
        [Tooltip("Per kind: water, drawn as its own surface.")]
        public bool[] water = new bool[0];
        [Tooltip("Per kind: land beyond the first build, drawn hazed.")]
        public bool[] beyond = new bool[0];
        [Tooltip("Texture pixels to a tile of painted ground.")]
        public int pixelsPerTile = 8;

        private GameObject _surface, _collider, _water;
        private Mesh _surfaceMesh, _colliderMesh, _waterMesh;
        private readonly List<Material> _waterMaterials = new List<Material>();
        private Heightfield _field;
        private bool _dirty;
        private Material _paintMaterial;
        private Texture2D _paintTexture;
        private System.Threading.Tasks.Task<byte[]> _painting;
        private int _paintWidth, _paintHeight;

        /// <summary>The ground's heights (flat when it has none).</summary>
        public Heightfield Field => _field ??= BuildField();

        /// <summary>Height of the ground at a point, if an enabled tile ground covers it.</summary>
        public static bool TryHeightAt(float x, float z, out float h)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                TileGround g = Active[i];
                if (g == null || g.width <= 0 || !g.Field.Contains(x, z)) continue;
                h = g.Field.Sample(x, z);
                return true;
            }
            h = 0f;
            return false;
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
            Rebuild();
        }

        private void OnDisable()
        {
            Active.Remove(this);
            Clear();
            if (_paintTexture != null) DestroyNow(_paintTexture);
            _paintTexture = null;
        }

        private void OnValidate()
        {
            _field = null;
            _dirty = true; // rebuilt in Update (after an undo, say): meshes can't change during OnValidate
        }

        private void Update()
        {
            if (_dirty) Rebuild();
            if (_painting != null && _painting.IsCompleted) ApplyPaint();
        }

        private Heightfield BuildField()
        {
            int w = Mathf.Max(1, width), d = Mathf.Max(1, height);
            return new Heightfield(originX, originY, w, d, corners != null && corners.Length == (w + 1) * (d + 1) ? corners : null);
        }

        /// <summary>Sets the heights (corners row by row from the south) and rebuilds.</summary>
        public void SetCorners(float[] heights)
        {
            corners = heights;
            _field = null;
            Rebuild();
        }

        /// <summary>Builds the meshes from the rows and heights: call after changing any field.</summary>
        public void Rebuild()
        {
            _dirty = false;
            Clear();
            _field = BuildField();
            int k = kinds.Length;
            if (width <= 0 || height <= 0 || k == 0 || materials.Length < k || lifts.Length < k || walkable.Length < k) return;
            if (painted && overlay.Length >= k && water.Length >= k)
            {
                RebuildPainted();
                return;
            }
            Heightfield f = _field;
            // Submeshes: one per kind, then the shared cliff, then each kind's own cliff.
            var mats = new List<Material>(materials.Take(k));
            int cliffSub = -1;
            if (cliffMaterial != null) { cliffSub = mats.Count; mats.Add(cliffMaterial); }
            var cliffOf = new int[k];
            for (int i = 0; i < k; i++)
            {
                Material own = i < cliffMaterials.Length ? cliffMaterials[i] : null;
                if (own != null) { cliffOf[i] = mats.Count; mats.Add(own); }
                else cliffOf[i] = cliffSub;
            }
            int subs = mats.Count;

            var kind = new int[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    kind[x, y] = -1;
            for (int r = 0; r < rows.Length && r < height; r++)
            {
                string row = rows[r] ?? "";
                int y = height - 1 - r;
                for (int x = 0; x < row.Length && x < width; x++) kind[x, y] = kinds.IndexOf(row[x]);
            }

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>[subs];
            for (int i = 0; i < subs; i++) tris[i] = new List<int>();
            var colliderVerts = new List<Vector3>();
            var colliderTris = new List<int>();

            float H(int cx, int cy) => f.Corner(originX + cx, originY + cy);
            Vector3 Normal(int cx, int cy)
            {
                float dx = (H(cx + 1, cy) - H(cx - 1, cy)) / 2f, dz = (H(cx, cy + 1) - H(cx, cy - 1)) / 2f;
                return new Vector3(-dx, 1f, -dz).normalized;
            }
            void Vertex(Vector3 p, Vector3 n)
            {
                verts.Add(p);
                normals.Add(n);
                uvs.Add(new Vector2(p.x, p.z + p.y));
            }

            // Tops: two triangles a tile, split along the south-west to north-east diagonal like
            // Heightfield.Sample, lit by the ground's slope so hills shade smoothly.
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int c = kind[x, y];
                    if (c < 0) continue;
                    float lift = lifts[c];
                    bool steep = f.IsSteep(originX + x, originY + y, cliffStep);
                    int sub = steep && cliffOf[c] >= 0 ? cliffOf[c] : c;
                    float ax = originX + x, az = originY + y;
                    Vector3 sw = new Vector3(ax, H(x, y) + lift, az), nw = new Vector3(ax, H(x, y + 1) + lift, az + 1);
                    Vector3 ne = new Vector3(ax + 1, H(x + 1, y + 1) + lift, az + 1), se = new Vector3(ax + 1, H(x + 1, y) + lift, az);
                    int v = verts.Count;
                    Vertex(sw, Normal(x, y)); Vertex(nw, Normal(x, y + 1)); Vertex(ne, Normal(x + 1, y + 1)); Vertex(se, Normal(x + 1, y));
                    tris[sub].AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
                    if (walkable[c] && !steep)
                    {
                        int cv = colliderVerts.Count;
                        colliderVerts.Add(sw); colliderVerts.Add(nw); colliderVerts.Add(ne); colliderVerts.Add(se);
                        colliderTris.AddRange(new[] { cv, cv + 1, cv + 2, cv, cv + 2, cv + 3 });
                    }
                }

            // Sides: each edge where a tile is lifted above its neighbour gets a face in the higher
            // tile's kind. At the region's edge a lifted tile drops to the ground, since the neighbour isn't known.
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int c = kind[x, y];
                    if (c < 0) continue;
                    float lift = lifts[c];
                    Side(x + 1, y, x + 1, y, x + 1, y + 1, Vector3.right);
                    Side(x - 1, y, x, y + 1, x, y, Vector3.left);
                    Side(x, y + 1, x + 1, y + 1, x, y + 1, Vector3.forward);
                    Side(x, y - 1, x, y, x + 1, y, Vector3.back);

                    // The edge runs from corner (ax, ay) to (bx, by), tile-local; the face looks along `normal`.
                    void Side(int nx, int ny, int ax, int ay, int bx, int by, Vector3 normal)
                    {
                        bool inside = nx >= 0 && ny >= 0 && nx < width && ny < height;
                        if (inside && kind[nx, ny] < 0) return;
                        float low = inside ? lifts[kind[nx, ny]] : Mathf.Min(lift, 0f);
                        if (low >= lift - 0.001f) return;
                        Vector3 a = new Vector3(originX + ax, H(ax, ay), originY + ay), b = new Vector3(originX + bx, H(bx, by), originY + by);
                        int v = verts.Count;
                        Vertex(a + Vector3.up * low, normal); Vertex(a + Vector3.up * lift, normal);
                        Vertex(b + Vector3.up * lift, normal); Vertex(b + Vector3.up * low, normal);
                        tris[c].AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
                    }
                }

            _surfaceMesh = new Mesh { name = "Tile ground", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            _surfaceMesh.SetVertices(verts);
            _surfaceMesh.SetNormals(normals);
            _surfaceMesh.SetUVs(0, uvs);
            _surfaceMesh.subMeshCount = subs;
            for (int i = 0; i < subs; i++) _surfaceMesh.SetTriangles(tris[i], i);
            _surfaceMesh.RecalculateBounds();
            _surface = Child("Surface");
            _surface.AddComponent<MeshFilter>().sharedMesh = _surfaceMesh;
            MeshRenderer surface = _surface.AddComponent<MeshRenderer>();
            surface.sharedMaterials = mats.ToArray();
            surface.enabled = _showSurface;

            if (colliderTris.Count > 0)
            {
                _colliderMesh = new Mesh { name = "Tile ground collider", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
                _colliderMesh.SetVertices(colliderVerts);
                _colliderMesh.SetTriangles(colliderTris, 0);
                _colliderMesh.RecalculateBounds();
                _collider = Child("Ground collider");
                _collider.AddComponent<MeshCollider>().sharedMesh = _colliderMesh;
            }
        }

        // ------------------------------------------------------------------ painted ground

        /// <summary>The kinds over the region and its margin, and how each looks to the painter.</summary>
        private GroundCanvas Canvas()
        {
            int k = kinds.Length;
            var looks = new GroundLook[k];
            var cliffs = new float[k * 3];
            Color rock = cliffMaterial != null ? cliffMaterial.color : new Color(0.42f, 0.4f, 0.37f);
            for (int i = 0; i < k; i++)
            {
                Color c = materials[i] != null ? materials[i].color : new Color(0.4f, 0.55f, 0.3f);
                // Water's bed: murky ground, near the shore's colour, seen through the shallows (BuildWater).
                if (water[i]) c = Color.Lerp(c, new Color(0.50f, 0.47f, 0.37f), 0.55f) * 0.8f;
                float[] rgb = { c.r, c.g, c.b };
                if (i < beyond.Length && beyond[i]) rgb = PaintedGround.Haze(rgb);
                GroundLayer layer = water[i] ? GroundLayer.Water : overlay[i] ? GroundLayer.Over : GroundLayer.Under;
                looks[i] = GroundLook.Of(rgb[0], rgb[1], rgb[2], layer, layer == GroundLayer.Over ? 1 + i : 1);
                looks[i].Shelf = !walkable[i] && !water[i] && lifts[i] > 0.3f;
                Color own = i < cliffMaterials.Length && cliffMaterials[i] != null ? cliffMaterials[i].color : rock;
                cliffs[i * 3] = own.r; cliffs[i * 3 + 1] = own.g; cliffs[i * 3 + 2] = own.b;
            }
            int[] grid = GroundPainter.Grid(rows, surround, width, height, margin, ch => kinds.IndexOf(ch));
            return new GroundCanvas
            {
                X0 = originX, Y0 = originY, Width = width, Height = height, Margin = margin,
                Kinds = grid, Looks = looks, Field = Field, CliffStep = cliffStep, CliffColours = cliffs,
                CliffColour = new[] { rock.r, rock.g, rock.b },
            };
        }

        /// <summary>
        /// The painted ground: one surface in one texture (GroundPainter), lifted where a kind is
        /// lifted (a rock shelf, a bridge), with water as its own surface; a tile whose corner is
        /// cut on the diagonal (GroundShapes) is two triangles along that diagonal, each its own
        /// kind, so shores and banks run at 45 degrees as the texture does. Collision stays per tile.
        /// </summary>
        private void RebuildPainted()
        {
            Heightfield f = _field;
            GroundCanvas canvas = Canvas();
            int cols = canvas.Cols, k = kinds.Length;
            GroundShapes.Compute(canvas.Kinds, cols, canvas.Rows, i => canvas.Looks[i].Shelf ? GroundLayer.Over : canvas.Looks[i].Layer,
                                 i => canvas.Looks[i].Shelf ? 1 << 21 : canvas.Looks[i].Group, out byte[] shape, out int[] other);
            Shore shore = Shore.Of(canvas, water);

            // Submesh 0 is the painted ground (the water's bed too); then one per kind, for faces.
            Material baseMat = null;
            for (int i = 0; i < k && baseMat == null; i++) if (!water[i] && materials[i] != null) baseMat = materials[i];
            if (baseMat == null) baseMat = materials[0];
            var mats = new List<Material> { baseMat };
            mats.AddRange(materials.Take(k));
            var tris = new List<int>[mats.Count];
            for (int i = 0; i < tris.Length; i++) tris[i] = new List<int>();
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var colliderVerts = new List<Vector3>();
            var colliderTris = new List<int>();

            float H(int cx, int cy) => f.Corner(originX + cx, originY + cy);
            float Dip(int cx, int cy) => shore.Dip(cx + margin, cy + margin);
            // The ground as drawn: its heights, dipping under the water along the shore.
            float Ground(int cx, int cy) => H(cx, cy) - Dip(cx, cy);
            Vector3 Normal(int cx, int cy)
            {
                float dx = (Ground(cx + 1, cy) - Ground(cx - 1, cy)) / 2f, dz = (Ground(cx, cy + 1) - Ground(cx, cy - 1)) / 2f;
                return new Vector3(-dx, 1f, -dz).normalized;
            }
            // -2: the painted ground beneath a cut (lift 0). The water's bed is the ground itself; its
            // surface is drawn on its own (BuildWater). Ground at its own height dips at the shore;
            // lifted kinds (a bridge, a rock shelf) don't.
            float Lift(int kind) => kind >= 0 && !water[kind] ? lifts[kind] : 0f;
            float Top(int kind, int cx, int cy) => H(cx, cy) + Lift(kind) - (Lift(kind) <= 0.02f ? Dip(cx, cy) : 0f);
            int Vertex(int cx, int cy, int kind)
            {
                verts.Add(new Vector3(originX + cx, Top(kind, cx, cy), originY + cy));
                normals.Add(Normal(cx, cy));
                uvs.Add(new Vector2((float)cx / width, (float)cy / height));
                return verts.Count - 1;
            }
            void Triangle(int kind, (int x, int y) a, (int x, int y) b, (int x, int y) c) =>
                tris[0].AddRange(new[] { Vertex(a.x, a.y, kind), Vertex(b.x, b.y, kind), Vertex(c.x, c.y, kind) });
            // A vertical face along an edge from corner a to corner b, from one kind's top up to a
            // higher kind's at each end (they differ where the ground dips), in the higher kind's material.
            void Face((int x, int y) a, (int x, int y) b, int low, int high, Vector3 normal)
            {
                float la = Top(low, a.x, a.y), ha = Top(high, a.x, a.y), lb = Top(low, b.x, b.y), hb = Top(high, b.x, b.y);
                if (ha <= la + 0.001f && hb <= lb + 0.001f) return;
                ha = Mathf.Max(ha, la);
                hb = Mathf.Max(hb, lb);
                var pa = new Vector3(originX + a.x, 0f, originY + a.y);
                var pb = new Vector3(originX + b.x, 0f, originY + b.y);
                int v = verts.Count;
                verts.Add(pa + Vector3.up * la); verts.Add(pa + Vector3.up * ha); verts.Add(pb + Vector3.up * hb); verts.Add(pb + Vector3.up * lb);
                for (int n = 0; n < 4; n++) { normals.Add(normal); uvs.Add(new Vector2((float)a.x / width, (float)a.y / height)); }
                tris[high >= 0 ? 1 + high : 0].AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
            }

            // A tile is two triangles, clockwise from above. Uncut, they split along the south-west to
            // north-east diagonal as Heightfield.Sample does. A cut at the south-west or north-east
            // corner splits along the other diagonal: A (SW, NW, SE) on the south and west edges, B
            // (SE, NW, NE) on the east and north. A cut at the north-west or south-east keeps the usual
            // split: A (SW, NW, NE) on the north and west edges, B (SW, NE, SE) on the south and east.
            // Returns the kind on each edge (S, E, N, W) and of each triangle.
            int[] Halves(int tx, int ty, out (int x, int y)[] a, out (int x, int y)[] b, out int ka, out int kb)
            {
                int gi = (ty + margin) * cols + tx + margin;
                int own = canvas.Kinds[gi];
                (int x, int y) sw = (tx, ty), se = (tx + 1, ty), ne = (tx + 1, ty + 1), nw = (tx, ty + 1);
                // A cut half shows the other kind, or for water cut back, the painted ground beneath.
                int cut = other[gi] >= 0 ? other[gi] : own >= 0 && water[own] ? -2 : own;
                switch (shape[gi])
                {
                    case GroundShapes.SouthWest: a = new[] { sw, nw, se }; b = new[] { se, nw, ne }; ka = cut; kb = own; return new[] { cut, own, own, cut };
                    case GroundShapes.NorthEast: a = new[] { sw, nw, se }; b = new[] { se, nw, ne }; ka = own; kb = cut; return new[] { own, cut, cut, own };
                    case GroundShapes.NorthWest: a = new[] { sw, nw, ne }; b = new[] { sw, ne, se }; ka = cut; kb = own; return new[] { own, own, cut, cut };
                    case GroundShapes.SouthEast: a = new[] { sw, nw, ne }; b = new[] { sw, ne, se }; ka = own; kb = cut; return new[] { cut, cut, own, own };
                    default: a = new[] { sw, nw, ne }; b = new[] { sw, ne, se }; ka = kb = own; return new[] { own, own, own, own };
                }
            }

            for (int ty = 0; ty < height; ty++)
                for (int tx = 0; tx < width; tx++)
                {
                    int gi = (ty + margin) * cols + tx + margin;
                    int own = canvas.Kinds[gi];
                    if (own < 0) continue;
                    int[] edge = Halves(tx, ty, out var t1, out var t2, out int k1, out int k2);
                    byte s = shape[gi];
                    Triangle(k1, t1[0], t1[1], t1[2]);
                    Triangle(k2, t2[0], t2[1], t2[2]);
                    // A face along the diagonal where the two halves stand at different heights. A face
                    // looks to the right of its edge's direction (from above), so it faces the lower half.
                    if (k1 != k2 && Mathf.Abs(Lift(k1) - Lift(k2)) > 0.001f)
                    {
                        bool antiDiagonal = s == GroundShapes.SouthWest || s == GroundShapes.NorthEast;
                        // North-west to south-east looks towards A; south-west to north-east towards B.
                        (int x, int y) a = antiDiagonal ? (tx, ty + 1) : (tx, ty), b = antiDiagonal ? (tx + 1, ty) : (tx + 1, ty + 1);
                        bool lowIsA = Lift(k1) < Lift(k2);
                        if (antiDiagonal != lowIsA) (a, b) = (b, a);
                        Vector3 along = new Vector3(b.x - a.x, 0f, b.y - a.y);
                        Face(a, b, lowIsA ? k1 : k2, lowIsA ? k2 : k1, new Vector3(along.z, 0f, -along.x).normalized);
                    }
                    // Faces on the tile's edges where this side stands above its neighbour's.
                    (int dx, int dy, int ax, int ay, int bx, int by, int mine, int theirs, Vector3 n)[] sides =
                    {
                        (0, -1, tx, ty, tx + 1, ty, 0, 2, Vector3.back),
                        (1, 0, tx + 1, ty, tx + 1, ty + 1, 1, 3, Vector3.right),
                        (0, 1, tx + 1, ty + 1, tx, ty + 1, 2, 0, Vector3.forward),
                        (-1, 0, tx, ty + 1, tx, ty, 3, 1, Vector3.left),
                    };
                    foreach (var side in sides)
                    {
                        int nx = tx + side.dx, ny = ty + side.dy;
                        int theirs;
                        if (nx >= 0 && ny >= 0 && nx < width && ny < height)
                        {
                            if (canvas.Kinds[(ny + margin) * cols + nx + margin] < 0) continue;
                            theirs = Halves(nx, ny, out _, out _, out _, out _)[side.theirs];
                        }
                        else
                        {
                            // Beyond the region: the margin's tile if it has one (without a margin, the ground).
                            int gx = nx + margin, gy = ny + margin;
                            int nk = gx >= 0 && gy >= 0 && gx < cols && gy < canvas.Rows ? canvas.Kinds[gy * cols + gx] : -1;
                            theirs = nk >= 0 ? nk : -1;
                        }
                        Face((side.ax, side.ay), (side.bx, side.by), theirs, edge[side.mine], side.n);
                    }
                    bool steep = f.IsSteep(originX + tx, originY + ty, cliffStep);
                    if (walkable[own] && !steep)
                    {
                        float ax = originX + tx, az = originY + ty;
                        int cv = colliderVerts.Count;
                        colliderVerts.Add(new Vector3(ax, Ground(tx, ty), az)); colliderVerts.Add(new Vector3(ax, Ground(tx, ty + 1), az + 1));
                        colliderVerts.Add(new Vector3(ax + 1, Ground(tx + 1, ty + 1), az + 1)); colliderVerts.Add(new Vector3(ax + 1, Ground(tx + 1, ty), az));
                        colliderTris.AddRange(new[] { cv, cv + 1, cv + 2, cv, cv + 2, cv + 3 });
                    }
                }

            _surfaceMesh = new Mesh { name = "Painted ground", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            _surfaceMesh.SetVertices(verts);
            _surfaceMesh.SetNormals(normals);
            _surfaceMesh.SetUVs(0, uvs);
            _surfaceMesh.subMeshCount = mats.Count;
            for (int i = 0; i < mats.Count; i++) _surfaceMesh.SetTriangles(tris[i], i);
            _surfaceMesh.RecalculateBounds();
            _surface = Child("Surface");
            _surface.AddComponent<MeshFilter>().sharedMesh = _surfaceMesh;
            MeshRenderer surface = _surface.AddComponent<MeshRenderer>();
            _paintMaterial = new Material(baseMat) { name = "Painted ground", hideFlags = HideFlags.DontSave, color = Color.white };
            mats[0] = _paintMaterial;
            surface.sharedMaterials = mats.ToArray();
            surface.enabled = _showSurface;

            BuildWater(canvas, shore, H);

            if (colliderTris.Count > 0)
            {
                _colliderMesh = new Mesh { name = "Tile ground collider", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
                _colliderMesh.SetVertices(colliderVerts);
                _colliderMesh.SetTriangles(colliderTris, 0);
                _colliderMesh.RecalculateBounds();
                _collider = Child("Ground collider");
                _collider.AddComponent<MeshCollider>().sharedMesh = _colliderMesh;
            }

            // The texture: painted off the main thread in play (it is plain maths), at once in the Editor.
            int ppt = Mathf.Clamp(pixelsPerTile, 1, 16);
            _paintWidth = width * ppt;
            _paintHeight = height * ppt;
            if (Application.isPlaying) _painting = System.Threading.Tasks.Task.Run(() => GroundPainter.Paint(canvas, ppt, out _, out _));
            else
            {
                _painting = System.Threading.Tasks.Task.FromResult(GroundPainter.Paint(canvas, ppt, out _, out _));
                ApplyPaint();
            }
        }

        private static Shader _waterShader;

        /// <summary>
        /// The water's surface (Water.shader): flat at its level over the water tiles and the ring of
        /// tiles round them. The bank dips under it (<see cref="Shore"/>) and hides it where it rises,
        /// so the waterline is where they meet. Each corner carries how shallow the water is there and
        /// which way and how fast it runs: down its level's slope (rivers), smoothed along the
        /// channel; lakes lie level and only ripple.
        /// </summary>
        private void BuildWater(GroundCanvas canvas, Shore shore, System.Func<int, int, float> H)
        {
            int cols = canvas.Cols, cc = shore.CornerCols, cr = shore.CornerRows;
            var level = new float[cc * cr];
            var has = new bool[cc * cr];
            for (int y = 0; y < cr; y++)
                for (int x = 0; x < cc; x++)
                {
                    int kind = shore.WaterAround(x, y);
                    if (kind < 0) continue;
                    level[y * cc + x] = H(x - margin, y - margin) + lifts[kind];
                    has[y * cc + x] = true;
                }
            if (!has.Any(h => h)) return;

            // Downhill along the level, smoothed a few times over the water so a river runs as one.
            var fx = new float[cc * cr];
            var fy = new float[cc * cr];
            float L(int x, int y, int i) => x >= 0 && y >= 0 && x < cc && y < cr && has[y * cc + x] ? level[y * cc + x] : level[i];
            for (int y = 0; y < cr; y++)
                for (int x = 0; x < cc; x++)
                {
                    int i = y * cc + x;
                    if (!has[i]) continue;
                    fx[i] = -(L(x + 1, y, i) - L(x - 1, y, i)) / 2f;
                    fy[i] = -(L(x, y + 1, i) - L(x, y - 1, i)) / 2f;
                }
            for (int pass = 0; pass < 3; pass++)
            {
                var sx = new float[cc * cr];
                var sy = new float[cc * cr];
                for (int y = 0; y < cr; y++)
                    for (int x = 0; x < cc; x++)
                    {
                        int i = y * cc + x;
                        if (!has[i]) continue;
                        float ax = 0f, ay = 0f;
                        int n = 0;
                        for (int oy = -2; oy <= 2; oy++)
                            for (int ox = -2; ox <= 2; ox++)
                            {
                                int nx = x + ox, ny = y + oy;
                                if (nx < 0 || ny < 0 || nx >= cc || ny >= cr || !has[ny * cc + nx]) continue;
                                ax += fx[ny * cc + nx];
                                ay += fy[ny * cc + nx];
                                n++;
                            }
                        sx[i] = ax / n;
                        sy[i] = ay / n;
                    }
                fx = sx;
                fy = sy;
            }

            // What a corner of the surface carries; a ring corner on dry land takes its wet neighbours'.
            (float level, Color data) At(int x, int y)
            {
                int i = y * cc + x;
                float lv = 0f, gx = 0f, gy = 0f;
                if (has[i])
                {
                    lv = level[i];
                    gx = fx[i];
                    gy = fy[i];
                }
                else
                {
                    int n = 0;
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int nx = x + ox, ny = y + oy;
                            if (nx < 0 || ny < 0 || nx >= cc || ny >= cr || !has[ny * cc + nx]) continue;
                            lv += level[ny * cc + nx];
                            gx += fx[ny * cc + nx];
                            gy += fy[ny * cc + nx];
                            n++;
                        }
                    if (n > 0) { lv /= n; gx /= n; gy /= n; }
                    // Under the land there, so the surface ends inside the ring tile, where it meets the bank.
                    lv = Mathf.Min(lv, H(x - margin, y - margin) - shore.Dip(x, y) - 0.05f);
                }
                var flow = new Vector2(gx, gy);
                float speed = Mathf.Clamp01(flow.magnitude * 25f); // a drop of 4 hundredths a tile runs at full speed
                Vector2 dir = flow.sqrMagnitude > 1e-10f ? flow.normalized : Vector2.zero;
                return (lv, new Color(shore.Shallow(x, y), dir.x * 0.5f + 0.5f, dir.y * 0.5f + 0.5f, speed));
            }

            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var subOf = new Dictionary<int, int>();
            var tris = new List<List<int>>();
            for (int ty = 0; ty < height; ty++)
                for (int tx = 0; tx < width; tx++)
                {
                    int x = tx + margin, y = ty + margin;
                    int own = canvas.Kinds[y * cols + x];
                    int kind = own >= 0 && water[own] ? own : -1;
                    if (kind < 0)
                        foreach ((int cx, int cy) in new[] { (x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1) })
                            if ((kind = shore.WaterAround(cx, cy)) >= 0) break;
                    if (kind < 0) continue;
                    if (!subOf.TryGetValue(kind, out int sub))
                    {
                        subOf[kind] = sub = tris.Count;
                        tris.Add(new List<int>());
                    }
                    int v = verts.Count;
                    foreach ((int cx, int cy) in new[] { (x, y), (x, y + 1), (x + 1, y + 1), (x + 1, y) })
                    {
                        (float lv, Color data) = At(cx, cy);
                        verts.Add(new Vector3(originX + cx - margin, lv, originY + cy - margin));
                        colors.Add(data);
                    }
                    tris[sub].AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
                }
            if (verts.Count == 0) return;

            _waterMesh = new Mesh { name = "Water", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            _waterMesh.SetVertices(verts);
            _waterMesh.SetColors(colors);
            _waterMesh.SetNormals(Enumerable.Repeat(Vector3.up, verts.Count).ToList());
            _waterMesh.subMeshCount = tris.Count;
            for (int i = 0; i < tris.Count; i++) _waterMesh.SetTriangles(tris[i], i);
            _waterMesh.RecalculateBounds();
            _water = Child("Water");
            _water.AddComponent<MeshFilter>().sharedMesh = _waterMesh;
            MeshRenderer renderer = _water.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (_waterShader == null) _waterShader = Resources.Load<Shader>("Shaders/Water");
            var mats = new Material[tris.Count];
            foreach (KeyValuePair<int, int> e in subOf)
            {
                Material own = materials[e.Key];
                Color deep = own != null ? own.color : new Color(0.22f, 0.36f, 0.48f);
                if (_waterShader == null)
                {
                    mats[e.Value] = own; // drawn flat, as before
                    continue;
                }
                var m = new Material(_waterShader) { name = "Water", hideFlags = HideFlags.DontSave };
                m.SetColor("_Color", deep);
                m.SetColor("_Shallow", Color.Lerp(deep, new Color(0.62f, 0.74f, 0.66f), 0.45f));
                _waterMaterials.Add(m);
                mats[e.Value] = m;
            }
            renderer.sharedMaterials = mats;
            renderer.enabled = _showSurface;
        }

        private void ApplyPaint()
        {
            System.Threading.Tasks.Task<byte[]> job = _painting;
            _painting = null;
            if (job == null || job.IsFaulted || _paintMaterial == null)
            {
                if (job?.Exception != null) Debug.LogException(job.Exception);
                return;
            }
            if (_paintTexture == null || _paintTexture.width != _paintWidth || _paintTexture.height != _paintHeight)
            {
                if (_paintTexture != null) DestroyNow(_paintTexture);
                _paintTexture = new Texture2D(_paintWidth, _paintHeight, TextureFormat.RGBA32, true)
                {
                    name = "Painted ground", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, anisoLevel = 2,
                };
            }
            _paintTexture.SetPixelData(job.Result, 0);
            _paintTexture.Apply(true);
            _paintMaterial.mainTexture = _paintTexture;
        }

        private static void DestroyNow(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        /// <summary>A child that is never saved with the scene: the meshes are rebuilt on load.</summary>
        private GameObject Child(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); // the vertices are in world tiles
            go.layer = gameObject.layer;
            return go;
        }

        private void Clear()
        {
            foreach (Object o in new Object[] { _surface, _collider, _water, _surfaceMesh, _colliderMesh, _waterMesh, _paintMaterial }.Concat(_waterMaterials))
            {
                if (o == null) continue;
                if (Application.isPlaying) Destroy(o);
                else DestroyImmediate(o);
            }
            _waterMaterials.Clear();
            _surface = _collider = _water = null;
            _surfaceMesh = _colliderMesh = _waterMesh = null;
            _paintMaterial = null;
            _painting = null; // a paint still running is dropped; the next build starts its own
        }
    }
}
