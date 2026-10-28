using System.Collections.Generic;
using Rth.Shared.World;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// The marker on the tile a click sends the player to: a rounded outline round the tile with a
    /// soft glow, a ripple when it is placed and a slow breath while it waits (Resources/Shaders/Marker),
    /// on a finely divided mesh draped over the ground there (hills, steps, a floor). It stays until
    /// the player is close, fading as they come within two tiles, and goes once they arrive or stop
    /// (the walk flag clears). Warm white for a walk, red for an action.
    /// </summary>
    public sealed class TileMarker : MonoBehaviour
    {
        private const int Cells = 6;                 // the drape's divisions across the tile
        private const float FadeOut = 0.3f;

        public GameSession session;
        public WorldView world;

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private Color _color;
        private float _shownAt = -10f, _hideFrom = -1f;
        private Vector3 _centre;
        private bool _visible;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int AgeId = Shader.PropertyToID("_Age");

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
            Drape(tile, plane);
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
                near = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 2f, d.magnitude));
            }
            float leaving = _hideFrom < 0f ? 1f : 1f - Mathf.Clamp01((now - _hideFrom) / FadeOut);
            float alpha = near * leaving;
            if (leaving <= 0f)
            {
                _visible = false;
                _renderer.enabled = false;
                return;
            }
            transform.position = _centre;
            _block.SetColor(ColorId, _color);
            _block.SetFloat(AlphaId, alpha);
            _block.SetFloat(AgeId, now - _shownAt);
            _renderer.SetPropertyBlock(_block);
            _renderer.enabled = alpha > 0.01f;
        }

        /// <summary>A tile-sized grid lying on the ground's height there (relative to the tile's centre), uv 0..1 across it.</summary>
        private void Drape(TileCoord tile, int plane)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int j = 0; j <= Cells; j++)
                for (int i = 0; i <= Cells; i++)
                {
                    float u = (float)i / Cells, v = (float)j / Cells;
                    float x = tile.X + u, z = tile.Y + v;
                    float y = GroundHeight.At(x, z, plane, _centre.y) - _centre.y + 0.03f;
                    verts.Add(new Vector3(u - 0.5f, y, v - 0.5f));
                    uvs.Add(new Vector2(u, v));
                }
            for (int j = 0; j < Cells; j++)
                for (int i = 0; i < Cells; i++)
                {
                    int a = j * (Cells + 1) + i, b = a + 1, c = a + Cells + 1, d = c + 1;
                    tris.AddRange(new[] { a, c, b, b, c, d });
                }
            _mesh.Clear();
            _mesh.SetVertices(verts);
            _mesh.SetUVs(0, uvs);
            _mesh.SetTriangles(tris, 0);
            _mesh.RecalculateBounds();
        }
    }
}
