using Rth.Shared.World;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// The marker on the tile a click sends the player to: four corner brackets and a dot, draped
    /// over the ground there (hills, steps, a floor), drawn by Resources/Shaders/Marker. It settles in
    /// from a little larger when placed, then stays, fading only as the player closes on it, and goes
    /// once they arrive or stop (the walk flag clears). Yellow for a walk, red for an action.
    /// </summary>
    public sealed class TileMarker : MonoBehaviour
    {
        private const float Settle = 0.18f;          // seconds to settle in
        private const float FadeOut = 0.25f;

        public GameSession session;
        public WorldView world;

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private Color _color;
        private float _shownAt = -10f, _alpha, _hideFrom = -1f;
        private Vector3 _centre;
        private bool _visible;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public static TileMarker Create(GameSession session, WorldView world)
        {
            var go = new GameObject("Tile Marker");
            var m = go.AddComponent<TileMarker>();
            m.session = session;
            m.world = world;
            m._mesh = new Mesh { name = "Tile Marker" };
            go.AddComponent<MeshFilter>().sharedMesh = m._mesh;
            m._renderer = go.AddComponent<MeshRenderer>();
            Shader shader = Resources.Load<Shader>("Shaders/Marker");
            m._renderer.sharedMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
            m._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m._renderer.receiveShadows = false;
            m._block = new MaterialPropertyBlock();
            m._renderer.enabled = false;
            return m;
        }

        public void Show(TileCoord tile, int plane, Color color)
        {
            _color = color;
            _centre = GroundHeight.Centre(tile, plane);
            Build(tile, plane);
            _shownAt = Time.unscaledTime;
            _hideFrom = -1f;
            _visible = true;
        }

        private void Update()
        {
            if (!_visible) return;
            float now = Time.unscaledTime;
            if (_hideFrom < 0f && session != null && session.WalkFlag == null && now - _shownAt > 0.3f) _hideFrom = now;

            // Proximity: full until the player is within two tiles, gone as they step onto it.
            float near = 1f;
            EntityView local = world != null ? world.Local : null;
            if (local != null)
            {
                Vector3 d = local.transform.position - _centre;
                d.y = 0f;
                near = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 2f, d.magnitude));
            }
            float settle = Mathf.Clamp01((now - _shownAt) / Settle);
            float leaving = _hideFrom < 0f ? 1f : 1f - Mathf.Clamp01((now - _hideFrom) / FadeOut);
            _alpha = near * leaving * Mathf.Lerp(0.4f, 1f, settle);
            if (leaving <= 0f)
            {
                _visible = false;
                _renderer.enabled = false;
                return;
            }
            float grow = 1f + 0.35f * (1f - settle) * (1f - settle);
            transform.position = _centre;
            transform.localScale = new Vector3(grow, 1f, grow);
            Color c = _color;
            c.a = _alpha;
            _block.SetColor(ColorId, c);
            _renderer.SetPropertyBlock(_block);
            _renderer.enabled = _alpha > 0.01f;
        }

        /// <summary>Corner brackets and a centre dot, each a strip of small quads lying on the ground's height (relative to the tile's centre).</summary>
        private void Build(TileCoord tile, int plane)
        {
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            float cx = tile.X + 0.5f, cz = tile.Y + 0.5f;

            void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
            {
                int i = verts.Count;
                foreach (Vector2 p in new[] { a, b, c, d })
                {
                    float y = GroundHeight.At(cx + p.x, cz + p.y, plane, _centre.y) - _centre.y + 0.035f;
                    verts.Add(new Vector3(p.x, y, p.y));
                }
                tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }

            // A bracket arm from p along dir, split in pieces so it follows the ground.
            void Arm(Vector2 p, Vector2 dir, Vector2 side, float length, float width)
            {
                const int pieces = 3;
                for (int k = 0; k < pieces; k++)
                {
                    Vector2 a = p + dir * (length * k / pieces), b = p + dir * (length * (k + 1) / pieces);
                    Quad(a, b, b + side * width, a + side * width);
                }
            }

            const float edge = 0.45f, arm = 0.26f, w = 0.1f;
            foreach (Vector2 corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
            {
                Vector2 p = corner * edge;
                Arm(p, new Vector2(-corner.x, 0), new Vector2(0, -corner.y), arm, w);
                Arm(p + new Vector2(0, -corner.y * w), new Vector2(0, -corner.y), new Vector2(-corner.x, 0), arm - w, w);
            }
            const float dot = 0.1f;
            Quad(new Vector2(0, -dot), new Vector2(dot, 0), new Vector2(0, dot), new Vector2(-dot, 0));

            _mesh.Clear();
            _mesh.SetVertices(verts);
            _mesh.SetTriangles(tris, 0);
            _mesh.RecalculateBounds();
        }
    }
}
