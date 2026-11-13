using System.Collections.Generic;
using Rth.Shared.Combat;
using Rth.Shared.World;
using UnityEngine;

namespace Rth.Client
{
    /// <summary>
    /// Creatures' special attacks winding up (the "telegraphs" server state): a square draped
    /// over the ground where each will land, coloured by its style, brightening and pulsing faster
    /// as it nears the tick it lands. A blow aimed at one target follows that target round. Drawn
    /// with the tile marker's shader (Resources/Shaders/Marker), so it reads as the same family of
    /// ground marks. Plain on purpose: the look gets its proper pass with the new art direction.
    /// </summary>
    public sealed class TelegraphViews : MonoBehaviour
    {
        private const int CellsPerTile = 3;

        public GameSession session;

        private int _version = -1;
        private readonly Dictionary<(int, string, uint), Mark> _marks = new Dictionary<(int, string, uint), Mark>();
        private readonly List<(int, string, uint)> _gone = new List<(int, string, uint)>();
        private Shader _shader;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        private static readonly int AgeId = Shader.PropertyToID("_Age");

        private sealed class Mark
        {
            public Telegraph Data;
            public GameObject Go;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public MaterialPropertyBlock Block;
            public float Born;
            public TileCoord DrapedAt;
            public bool Draped;
        }

        private void Update()
        {
            if (session == null || !session.IsInGame)
            {
                Clear();
                return;
            }
            if (session.StateVersion != _version)
            {
                _version = session.StateVersion;
                Sync(session.ServerState<TelegraphsState>(TelegraphsState.Key));
            }
            float tick = session.EstimatedTick;
            foreach (Mark mark in _marks.Values) Animate(mark, tick);
        }

        private void Sync(TelegraphsState state)
        {
            var live = new HashSet<(int, string, uint)>();
            foreach (Telegraph t in state.Telegraphs)
            {
                var key = (t.NpcId, t.Move, t.StartTick);
                live.Add(key);
                if (!_marks.ContainsKey(key)) _marks[key] = Create(t);
            }
            _gone.Clear();
            foreach (var key in _marks.Keys)
                if (!live.Contains(key)) _gone.Add(key);
            foreach (var key in _gone)
            {
                Destroy(_marks[key].Go);
                Destroy(_marks[key].Mesh);
                _marks.Remove(key);
            }
        }

        private Mark Create(Telegraph t)
        {
            if (_shader == null) _shader = Resources.Load<Shader>("Shaders/Marker") ?? Shader.Find("Sprites/Default");
            var go = new GameObject("Telegraph " + t.Move);
            var mesh = new Mesh { name = "Telegraph" };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(_shader) { hideFlags = HideFlags.DontSave };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return new Mark { Data = t, Go = go, Mesh = mesh, Renderer = renderer, Block = new MaterialPropertyBlock(), Born = Time.unscaledTime };
        }

        private void Animate(Mark mark, float tick)
        {
            Telegraph t = mark.Data;
            // Where it is now: its fixed square, or under the target it follows.
            var tile = new TileCoord(t.X, t.Y);
            if (t.TargetId > 0 && session.world != null)
            {
                EntityView target = session.world.Find(t.TargetId);
                if (target != null) tile = new TileCoord(Mathf.FloorToInt(target.transform.position.x), Mathf.FloorToInt(target.transform.position.z));
            }
            if (!mark.Draped || mark.DrapedAt != tile)
            {
                Drape(mark, tile, t.Radius, t.Plane);
                mark.DrapedAt = tile;
                mark.Draped = true;
            }

            float span = Mathf.Max(1f, t.LandTick - t.StartTick);
            float progress = Mathf.Clamp01((tick - t.StartTick) / span);
            Color color = StyleColor(t.Style);
            mark.Block.SetColor(ColorId, Color.Lerp(color * 0.7f, color, progress));
            mark.Block.SetFloat(AlphaId, Mathf.Lerp(0.45f, 1f, progress));
            // The marker's breath quickens as the blow comes: age runs faster near the end.
            mark.Block.SetFloat(AgeId, (Time.unscaledTime - mark.Born) * (1f + 3f * progress));
            mark.Renderer.SetPropertyBlock(mark.Block);
        }

        private static Color StyleColor(CombatStyle style) => style switch
        {
            CombatStyle.Melee => new Color(0.92f, 0.25f, 0.18f),
            CombatStyle.Ranged => new Color(0.45f, 0.85f, 0.3f),
            CombatStyle.Occult => new Color(0.98f, 0.8f, 0.2f),
            _ => new Color(0.95f, 0.95f, 0.95f),
        };

        /// <summary>A grid over the square of <paramref name="radius"/> round a tile, lying on the ground there, uv 0..1 across it.</summary>
        private static void Drape(Mark mark, TileCoord centre, int radius, int plane)
        {
            int side = 2 * radius + 1;
            int cells = side * CellsPerTile;
            float x0 = centre.X - radius, z0 = centre.Y - radius;
            Vector3 origin = GroundHeight.Centre(centre, plane);
            mark.Go.transform.position = origin;
            var verts = new List<Vector3>((cells + 1) * (cells + 1));
            var uvs = new List<Vector2>(verts.Capacity);
            var tris = new List<int>(cells * cells * 6);
            for (int j = 0; j <= cells; j++)
                for (int i = 0; i <= cells; i++)
                {
                    float u = (float)i / cells, v = (float)j / cells;
                    float x = x0 + u * side, z = z0 + v * side;
                    float y = GroundHeight.At(x, z, plane, origin.y) - origin.y + 0.04f;
                    verts.Add(new Vector3(x - origin.x, y, z - origin.z));
                    uvs.Add(new Vector2(u, v));
                }
            for (int j = 0; j < cells; j++)
                for (int i = 0; i < cells; i++)
                {
                    int a = j * (cells + 1) + i, b = a + 1, c = a + cells + 1, d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            mark.Mesh.Clear();
            mark.Mesh.SetVertices(verts);
            mark.Mesh.SetUVs(0, uvs);
            mark.Mesh.SetTriangles(tris, 0);
            mark.Mesh.RecalculateBounds();
        }

        private void Clear()
        {
            foreach (Mark mark in _marks.Values)
            {
                Destroy(mark.Go);
                Destroy(mark.Mesh);
            }
            _marks.Clear();
        }

        private void OnDestroy() => Clear();
    }
}
