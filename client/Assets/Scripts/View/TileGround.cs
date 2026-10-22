using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rth.Client
{
    /// <summary>
    /// Ground drawn tile by tile from a region's "tiles" piece (world/regions, docs/workflow.md): one
    /// mesh with a submesh per kind, tops merged into rectangles, sides where a tile stands higher
    /// than its neighbour (a rock shelf, a bank down to water), and a collider over the walkable
    /// tiles only, which is all the map exporter and <see cref="GroundHeight"/> count as ground.
    /// The scene keeps only the rows; the meshes are rebuilt whenever it loads, so they cost the
    /// scene file nothing.
    /// </summary>
    [ExecuteAlways]
    public sealed class TileGround : MonoBehaviour
    {
        [Tooltip("Absolute tile of the south-west corner.")]
        public int originX, originY;
        public int width, height;
        [Tooltip("One string a row from the north, a character a tile from the west; a space is no ground.")]
        public string[] rows = new string[0];

        [Tooltip("The characters the rows use, one per kind.")]
        public string kinds = "";
        public Material[] materials = new Material[0];
        [Tooltip("Where each kind's top is drawn, in metres.")]
        public float[] heights = new float[0];
        [Tooltip("Kinds that are walkable ground (the rest block: rock, water).")]
        public bool[] walkable = new bool[0];

        private GameObject _surface, _collider;
        private Mesh _surfaceMesh, _colliderMesh;

        private void OnEnable() => Rebuild();

        private void OnDisable() => Clear();

        /// <summary>Builds the meshes from the rows: call after changing any field.</summary>
        public void Rebuild()
        {
            Clear();
            int k = kinds.Length;
            if (width <= 0 || height <= 0 || k == 0 || materials.Length < k || heights.Length < k || walkable.Length < k) return;

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
            var tris = new List<int>[k];
            for (int i = 0; i < k; i++) tris[i] = new List<int>();
            var colliderVerts = new List<Vector3>();
            var colliderTris = new List<int>();

            void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, bool uv)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                if (uv)
                {
                    normals.Add(normal); normals.Add(normal); normals.Add(normal); normals.Add(normal);
                    uvs.Add(new Vector2(a.x, a.z + a.y)); uvs.Add(new Vector2(b.x, b.z + b.y));
                    uvs.Add(new Vector2(c.x, c.z + c.y)); uvs.Add(new Vector2(d.x, d.z + d.y));
                }
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
                t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }

            // Tops, merged into rectangles of one kind.
            var used = new bool[width, height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int c = kind[x, y];
                    if (c < 0 || used[x, y]) continue;
                    int x1 = x + 1;
                    while (x1 < width && kind[x1, y] == c && !used[x1, y]) x1++;
                    int y1 = y + 1;
                    while (y1 < height && RunIs(kind, used, x, x1, y1, c)) y1++;
                    for (int yy = y; yy < y1; yy++)
                        for (int xx = x; xx < x1; xx++) used[xx, yy] = true;
                    float h = heights[c];
                    float ax = originX + x, az = originY + y, bx = originX + x1, bz = originY + y1;
                    Vector3 sw = new Vector3(ax, h, az), nw = new Vector3(ax, h, bz), ne = new Vector3(bx, h, bz), se = new Vector3(bx, h, az);
                    Quad(verts, tris[c], sw, nw, ne, se, Vector3.up, true);
                    if (walkable[c]) Quad(colliderVerts, colliderTris, sw, nw, ne, se, Vector3.up, false);
                }

            // Sides: each edge where a tile stands above its neighbour gets a face in the higher tile's
            // kind. At the region's edge a raised tile drops to sea level, since the neighbour isn't known.
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int c = kind[x, y];
                    if (c < 0) continue;
                    float h = heights[c];
                    float wx = originX + x, wz = originY + y;
                    Side(x + 1, y, new Vector3(wx + 1, 0, wz), new Vector3(wx + 1, 0, wz + 1), Vector3.right);
                    Side(x - 1, y, new Vector3(wx, 0, wz + 1), new Vector3(wx, 0, wz), Vector3.left);
                    Side(x, y + 1, new Vector3(wx + 1, 0, wz + 1), new Vector3(wx, 0, wz + 1), Vector3.forward);
                    Side(x, y - 1, new Vector3(wx, 0, wz), new Vector3(wx + 1, 0, wz), Vector3.back);

                    void Side(int nx, int ny, Vector3 a, Vector3 b, Vector3 normal)
                    {
                        bool inside = nx >= 0 && ny >= 0 && nx < width && ny < height;
                        float low = inside ? (kind[nx, ny] < 0 ? h : heights[kind[nx, ny]]) : Mathf.Min(h, 0f);
                        if (low >= h - 0.001f) return;
                        Quad(verts, tris[c], new Vector3(a.x, low, a.z), new Vector3(a.x, h, a.z), new Vector3(b.x, h, b.z), new Vector3(b.x, low, b.z), normal, true);
                    }
                }

            _surfaceMesh = new Mesh { name = "Tile ground", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            _surfaceMesh.SetVertices(verts);
            _surfaceMesh.SetNormals(normals);
            _surfaceMesh.SetUVs(0, uvs);
            _surfaceMesh.subMeshCount = k;
            for (int i = 0; i < k; i++) _surfaceMesh.SetTriangles(tris[i], i);
            _surfaceMesh.RecalculateBounds();
            _surface = Child("Surface");
            _surface.AddComponent<MeshFilter>().sharedMesh = _surfaceMesh;
            var renderer = _surface.AddComponent<MeshRenderer>();
            var mats = new Material[k];
            for (int i = 0; i < k; i++) mats[i] = materials[i];
            renderer.sharedMaterials = mats;

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

        private static bool RunIs(int[,] kind, bool[,] used, int x0, int x1, int y, int c)
        {
            for (int x = x0; x < x1; x++)
                if (kind[x, y] != c || used[x, y]) return false;
            return true;
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
