using System.Collections.Generic;
using System.Linq;
using Rth.Shared.World;
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
    /// and marks it <see cref="sculpted"/>; saving the scene writes the heights back to the data.
    /// </summary>
    [ExecuteAlways]
    public sealed class TileGround : MonoBehaviour
    {
        /// <summary>Every enabled tile ground, for height lookups (the camera's terrain lift).</summary>
        public static readonly List<TileGround> Active = new List<TileGround>();

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

        private GameObject _surface, _collider;
        private Mesh _surfaceMesh, _colliderMesh;
        private Heightfield _field;
        private bool _dirty;

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
        }

        private void OnValidate()
        {
            _field = null;
            _dirty = true; // rebuilt in Update (after an undo, say): meshes can't change during OnValidate
        }

        private void Update()
        {
            if (_dirty) Rebuild();
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
            _surface.AddComponent<MeshRenderer>().sharedMaterials = mats.ToArray();

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
            foreach (Object o in new Object[] { _surface, _collider, _surfaceMesh, _colliderMesh })
            {
                if (o == null) continue;
                if (Application.isPlaying) Destroy(o);
                else DestroyImmediate(o);
            }
            _surface = _collider = null;
            _surfaceMesh = _colliderMesh = null;
        }
    }
}
