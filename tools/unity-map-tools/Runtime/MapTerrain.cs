using System.Collections.Generic;
using Rth.Shared.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rth.MapTools
{
    /// <summary>
    /// Ground with height: a <see cref="Heightfield"/> over a rectangle of tiles (one height per
    /// tile corner) and a colour per tile, blended over <see cref="blend"/> tiles so grass shades
    /// into mud with no edge. It builds its own mesh (two triangles a tile, smooth light) and
    /// collider, so the exporter treats it as ground like any other collider, and it blocks
    /// every tile steeper than <see cref="cliffStep"/> (a cliff). Keep its transform at the
    /// world origin: heights and tiles are absolute.
    /// The client stands characters on it (<c>GroundHeight</c>) and the camera tilts up over it.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class MapTerrain : MonoBehaviour
    {
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        /// <summary>Every enabled terrain, for height lookups.</summary>
        public static readonly List<MapTerrain> Active = new List<MapTerrain>();

        [Tooltip("Absolute tile of the south-west corner.")]
        public Vector2Int origin;
        public Vector2Int size = new Vector2Int(32, 32);

        [Tooltip("Corner heights in tiles, (size.x + 1) × (size.y + 1), row by row from the south. Written by builders.")]
        [HideInInspector] public float[] heights = new float[0];

        [Tooltip("A colour per tile, size.x × size.y, row by row from the south. Empty: the base colour everywhere.")]
        [HideInInspector] public Color32[] tileColors = new Color32[0];

        [Tooltip("Tiles nobody walks on (the mountain wall, the frozen north), size.x × size.y. Empty: none. The exporter blocks them.")]
        [HideInInspector] public bool[] blocked = new bool[0];

        public Color baseColor = new Color(0.36f, 0.55f, 0.22f);
        [Tooltip("Colour of tiles too steep to walk (cliff faces).")]
        public Color cliffColor = new Color(0.42f, 0.4f, 0.36f);
        [Tooltip("How many tiles colours blend over (0: crisp tiles).")]
        [Range(0, 6)] public int blend = 2;
        [Tooltip("A tile rising more than this many tiles across is a cliff: drawn in the cliff colour and blocked.")]
        public float cliffStep = 1f;

        private Heightfield _field;
        private Mesh _mesh;
        private Texture2D _colors;
        private MaterialPropertyBlock _block;
        private bool _dirty = true;

        /// <summary>The heights, as the exporter and the client read them.</summary>
        public Heightfield Field
        {
            get
            {
                if (_field == null) _field = BuildField();
                return _field;
            }
        }

        /// <summary>Height of the ground at a point, if any enabled terrain covers it.</summary>
        public static bool TryHeightAt(float x, float z, out float height)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                MapTerrain t = Active[i];
                if (t == null || !t.Field.Contains(x, z)) continue;
                height = t.Field.Sample(x, z) + t.transform.position.y;
                return true;
            }
            height = 0f;
            return false;
        }

        /// <summary>Whether a tile is marked impassable (cliffs are separate: see <see cref="cliffStep"/>).</summary>
        public bool IsBlocked(int x, int y)
        {
            int i = x - origin.x, j = y - origin.y;
            return blocked != null && blocked.Length == size.x * size.y && i >= 0 && i < size.x && j >= 0 && j < size.y && blocked[j * size.x + i];
        }

        /// <summary>Replaces the heights (and optionally the colours and impassable tiles) and rebuilds. For builders.</summary>
        public void Set(Heightfield field, Color32[] colors = null, bool[] impassable = null)
        {
            origin = new Vector2Int(field.OriginX, field.OriginY);
            size = new Vector2Int(field.Width, field.Depth);
            heights = field.Corners;
            tileColors = colors ?? new Color32[0];
            blocked = impassable ?? new bool[0];
            _field = null;
            Rebuild();
        }

        private Heightfield BuildField()
        {
            int corners = (size.x + 1) * (size.y + 1);
            return new Heightfield(origin.x, origin.y, Mathf.Max(1, size.x), Mathf.Max(1, size.y),
                heights != null && heights.Length == corners ? heights : null);
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
            Rebuild();
        }

        private void OnDisable() => Active.Remove(this);

        private void OnValidate()
        {
            _field = null;
            _dirty = true; // rebuilt in Update: meshes and colliders can't change during OnValidate
        }

        private void Update()
        {
            if (_dirty) Rebuild();
        }

        private void OnDestroy()
        {
            Release(_mesh);
            Release(_colors);
        }

        private static void Release(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }

        /// <summary>Rebuilds the mesh, collider and colour texture from the data.</summary>
        public void Rebuild()
        {
            _dirty = false;
            _field = BuildField();
            Heightfield f = _field;
            int w = f.Width, d = f.Depth;

            if (_mesh == null) _mesh = new Mesh { name = "MapTerrain", hideFlags = HideFlags.DontSave };
            _mesh.Clear();
            var vertices = new Vector3[(w + 1) * (d + 1)];
            var uvs = new Vector2[vertices.Length];
            for (int j = 0; j <= d; j++)
                for (int i = 0; i <= w; i++)
                {
                    int k = j * (w + 1) + i;
                    vertices[k] = new Vector3(f.OriginX + i, f.Corner(f.OriginX + i, f.OriginY + j), f.OriginY + j);
                    uvs[k] = new Vector2(i / (float)w, j / (float)d);
                }
            var triangles = new int[w * d * 6];
            int t = 0;
            for (int j = 0; j < d; j++)
                for (int i = 0; i < w; i++)
                {
                    int sw = j * (w + 1) + i, se = sw + 1, nw = sw + w + 1, ne = nw + 1;
                    // Split along the south-west to north-east diagonal, as Heightfield.Sample does.
                    triangles[t++] = sw; triangles[t++] = nw; triangles[t++] = ne;
                    triangles[t++] = sw; triangles[t++] = ne; triangles[t++] = se;
                }
            _mesh.indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _mesh.vertices = vertices;
            _mesh.uv = uvs;
            _mesh.triangles = triangles;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            MeshCollider meshCollider = GetComponent<MeshCollider>();
            meshCollider.sharedMesh = null; // forces the collider to re-cook the changed mesh
            meshCollider.sharedMesh = _mesh;

            BuildColors(f);
        }

        private void BuildColors(Heightfield f)
        {
            int w = f.Width, d = f.Depth;
            var source = new Color[w * d];
            bool hasTiles = tileColors != null && tileColors.Length == w * d;
            for (int k = 0; k < source.Length; k++) source[k] = hasTiles ? (Color)tileColors[k] : baseColor;

            // Box blur, one pass per axis: each tile becomes the average of its neighbours within `blend`.
            Color[] blurred = Blur(Blur(source, w, d, blend, true), w, d, blend, false);
            for (int j = 0; j < d; j++)
                for (int i = 0; i < w; i++)
                    if (f.IsSteep(f.OriginX + i, f.OriginY + j, cliffStep)) blurred[j * w + i] = cliffColor; // cliffs stay crisp

            if (_colors == null || _colors.width != w || _colors.height != d)
            {
                Release(_colors);
                _colors = new Texture2D(w, d, TextureFormat.RGBA32, false) { name = "MapTerrain colours", hideFlags = HideFlags.DontSave };
            }
            _colors.wrapMode = TextureWrapMode.Clamp;
            _colors.filterMode = FilterMode.Bilinear;
            _colors.SetPixels(blurred);
            _colors.Apply(false);

            var r = GetComponent<MeshRenderer>();
            if (_block == null) _block = new MaterialPropertyBlock();
            r.GetPropertyBlock(_block);
            _block.SetTexture(BaseMap, _colors);
            _block.SetColor(BaseColor, Color.white);
            r.SetPropertyBlock(_block);
        }

        private static Color[] Blur(Color[] src, int w, int d, int radius, bool alongX)
        {
            if (radius <= 0) return src;
            var dst = new Color[src.Length];
            for (int j = 0; j < d; j++)
                for (int i = 0; i < w; i++)
                {
                    Color sum = Color.clear;
                    int n = 0;
                    for (int o = -radius; o <= radius; o++)
                    {
                        int x = alongX ? i + o : i, y = alongX ? j : j + o;
                        if (x < 0 || x >= w || y < 0 || y >= d) continue;
                        sum += src[y * w + x];
                        n++;
                    }
                    dst[j * w + i] = sum / n;
                }
            return dst;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.5f, 0.2f, 0.8f);
            Heightfield f = Field;
            for (int y = f.OriginY; y < f.OriginY + f.Depth; y++)
                for (int x = f.OriginX; x < f.OriginX + f.Width; x++)
                    if (f.IsSteep(x, y, cliffStep))
                        Gizmos.DrawWireCube(new Vector3(x + 0.5f, f.TileCentre(x, y), y + 0.5f), new Vector3(1f, 0.05f, 1f));
        }
    }
}
