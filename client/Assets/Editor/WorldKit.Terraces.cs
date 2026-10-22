using System.Collections.Generic;
using Rth.MapTools;
using UnityEngine;

namespace Rth.Client.Editor
{
    /// <summary>
    /// Ground that climbs: a city on terraces, the stairs and ramps between them, bridges over a
    /// gorge. Describe the ground tile by tile (terraces, stairs, ramps, bridge decks and voids),
    /// then <see cref="Build"/> it in one go. The server is flat and the exporter ignores height,
    /// so this is also what keeps the two honest: every edge between two tiles whose ground differs
    /// by more than a step gets a <see cref="MapWall"/> (drawn as a kerb or a parapet on the upper
    /// side), and stairs and ramps are checked against the gradient rules (a step at most 0.5 m, a
    /// ramp at most 1 m in 3 tiles). Tiles nobody described have no ground: the gorge, the sea, the
    /// cliffs. Everything here is on plane 0; floors above the ground are MapLevels.
    /// Coordinates are absolute tiles, like the rest of the kit.
    /// </summary>
    public sealed class Terraces
    {
        /// <summary>The highest step that needs no wall; anything taller is a drop.</summary>
        public const float MaxStep = 0.5f;

        /// <summary>The steepest a ramp may climb: 1 m in 3 tiles.</summary>
        public const float MaxGradient = 1f / 3f;

        /// <summary>Where every terrace's retaining wall starts, below the sea.</summary>
        public const float Bottom = -4f;

        private enum Kind : byte
        {
            None,
            Flat,
            Stair,
            Ramp,
            Deck,
        }

        private readonly RectInt _area;
        private readonly float[,] _height;
        private readonly Kind[,] _kind;
        private readonly Material[,] _surface;
        private readonly List<(RectInt Rect, WorldKit.Side Up, float From, float To, Material Surface)> _ramps =
            new List<(RectInt, WorldKit.Side, float, float, Material)>();

        public Terraces(Vector2Int origin, Vector2Int size)
        {
            _area = new RectInt(origin, size);
            _height = new float[size.x, size.y];
            _kind = new Kind[size.x, size.y];
            _surface = new Material[size.x, size.y];
        }

        // ------------------------------------------------------------------ describing the ground

        /// <summary>A flat terrace over <paramref name="r"/> at <paramref name="height"/> metres (replaces what was there).</summary>
        public void Flat(RectInt r, float height, Material surface) => Set(r, (x, z) => height, Kind.Flat, surface);

        /// <summary>No ground over <paramref name="r"/>: a gorge, the sea, a cliff. Blocked on the server.</summary>
        public void Void(RectInt r) => Set(r, (x, z) => 0f, Kind.None, null);

        /// <summary>
        /// A stair over <paramref name="r"/> climbing towards <paramref name="up"/> from
        /// <paramref name="from"/> to <paramref name="to"/> metres, one step per row of tiles, with a
        /// flat landing every <paramref name="landingEvery"/> steps (0 for none). Warns if a step is
        /// taller than <see cref="MaxStep"/>.
        /// </summary>
        public void Stair(RectInt r, WorldKit.Side up, float from, float to, Material step, int landingEvery = 0)
        {
            int rows = Rows(r, up);
            int landings = landingEvery > 0 ? (rows - 1) / (landingEvery + 1) : 0;
            int steps = rows - landings;
            float rise = (to - from) / steps;
            if (Mathf.Abs(rise) > MaxStep + 0.001f)
                Debug.LogWarning($"[R'th] Stair at {r.x},{r.y} climbs {rise:0.00} m a step (at most {MaxStep}); make it longer.");
            var heights = new float[rows];
            float h = from;
            for (int i = 0, run = 0; i < rows; i++)
            {
                bool landing = landingEvery > 0 && run == landingEvery && i < rows - 1;
                if (landing) run = 0;
                else
                {
                    h += rise;
                    run++;
                }
                heights[i] = h;
            }
            Set(r, (x, z) => heights[Row(r, up, x, z)], Kind.Stair, step);
        }

        /// <summary>
        /// A ramp over <paramref name="r"/> climbing towards <paramref name="up"/> from
        /// <paramref name="from"/> (at its lower edge) to <paramref name="to"/> metres (at its upper
        /// edge). Warns if it is steeper than <see cref="MaxGradient"/>.
        /// </summary>
        public void Ramp(RectInt r, WorldKit.Side up, float from, float to, Material surface)
        {
            int rows = Rows(r, up);
            if (Mathf.Abs(to - from) / rows > MaxGradient + 0.001f)
                Debug.LogWarning($"[R'th] Ramp at {r.x},{r.y} climbs {(to - from) / rows:0.00} m a tile (at most {MaxGradient:0.00}); make it longer or use a stair.");
            Set(r, (x, z) => from + (to - from) * (Row(r, up, x, z) + 0.5f) / rows, Kind.Ramp, surface);
            _ramps.Add((r, up, from, to, surface));
        }

        /// <summary>A bridge deck over <paramref name="r"/> at <paramref name="height"/>: ground over a void. Draw what holds it up with <see cref="WorldKit.Viaduct"/>.</summary>
        public void Deck(RectInt r, float height, Material deck) => Set(r, (x, z) => height, Kind.Deck, deck);

        /// <summary>The ground's height at a tile, or null where there is none.</summary>
        public float? Height(int x, int z)
        {
            if (!_area.Contains(new Vector2Int(x, z))) return null;
            int lx = x - _area.x, lz = z - _area.y;
            return _kind[lx, lz] == Kind.None ? (float?)null : _height[lx, lz];
        }

        /// <summary>A point on the ground at a (fractional) tile position: the height of the tile under it, or 0 where there is none.</summary>
        public Vector3 On(float x, float z, float lift = 0f) =>
            new Vector3(x, (Height(Mathf.FloorToInt(x), Mathf.FloorToInt(z)) ?? 0f) + lift, z);

        /// <summary>A recoloured patch on a flat terrace (a plaza, a lawn, a street), following its height.</summary>
        public GameObject Patch(string name, Transform parent, int x0, int z0, int x1, int z1, Material material) =>
            WorldKit.Patch(name, parent, x0, z0, x1, z1, material, (Height(x0, z0) ?? 0f) + 0.006f);

        /// <summary>How much of the region is ground anyone can stand on.</summary>
        public int GroundTiles
        {
            get
            {
                int n = 0;
                foreach (Kind k in _kind)
                    if (k != Kind.None) n++;
                return n;
            }
        }

        // ------------------------------------------------------------------ building it

        /// <summary>
        /// Builds the ground: terraces as solid blocks (a stone body under a surface cap), stairs as
        /// steps, ramps as sloping slabs over a stepped fill, decks as slabs; then a MapWall on every
        /// drop, drawn as a kerb, or a parapet where the drop is tall or falls into nothing.
        /// </summary>
        public Transform Build(Transform parent, Material retain, Material parapet)
        {
            Transform root = WorldKit.Group(parent, "Terrain");
            BuildFlats(WorldKit.Group(root, "Terraces"), retain);
            BuildStairs(WorldKit.Group(root, "Stairs"), retain);
            BuildRamps(WorldKit.Group(root, "Ramps"), retain);
            BuildDecks(WorldKit.Group(root, "Decks"));
            BuildEdges(WorldKit.Group(root, "Edges"), parapet);
            return root;
        }

        private void BuildFlats(Transform group, Material retain)
        {
            var done = new bool[_area.width, _area.height];
            for (int lz = 0; lz < _area.height; lz++)
                for (int lx = 0; lx < _area.width; lx++)
                {
                    if (done[lx, lz] || _kind[lx, lz] != Kind.Flat) continue;
                    // Grow the largest rectangle of the same height and surface from here: first along x, then rows.
                    float h = _height[lx, lz];
                    Material m = _surface[lx, lz];
                    bool Same(int x, int z) => !done[x, z] && _kind[x, z] == Kind.Flat && Mathf.Approximately(_height[x, z], h) && _surface[x, z] == m;
                    int w = 1;
                    while (lx + w < _area.width && Same(lx + w, lz)) w++;
                    int d = 1;
                    while (lz + d < _area.height)
                    {
                        bool row = true;
                        for (int i = 0; i < w && row; i++) row = Same(lx + i, lz + d);
                        if (!row) break;
                        d++;
                    }
                    for (int j = 0; j < d; j++)
                        for (int i = 0; i < w; i++)
                            done[lx + i, lz + j] = true;
                    Block(group, "Terrace", _area.x + lx, _area.y + lz, w, d, h, m, retain);
                }
        }

        private void BuildStairs(Transform group, Material retain)
        {
            // One box per run of equal-height stair tiles along x (a step, or part of one).
            for (int lz = 0; lz < _area.height; lz++)
                for (int lx = 0; lx < _area.width;)
                {
                    if (_kind[lx, lz] != Kind.Stair)
                    {
                        lx++;
                        continue;
                    }
                    float h = _height[lx, lz];
                    int w = 1;
                    while (lx + w < _area.width && _kind[lx + w, lz] == Kind.Stair && Mathf.Approximately(_height[lx + w, lz], h)) w++;
                    Block(group, "Step", _area.x + lx, _area.y + lz, w, 1, h, _surface[lx, lz], retain);
                    lx += w;
                }
        }

        private void BuildRamps(Transform group, Material retain)
        {
            foreach ((RectInt r, WorldKit.Side up, float from, float to, Material surface) in _ramps)
            {
                Transform ramp = WorldKit.Group(group, "Ramp");
                bool alongZ = up == WorldKit.Side.North || up == WorldKit.Side.South;
                int rows = alongZ ? r.height : r.width;
                // The fill under it: one block per row, up to that row's lower edge.
                for (int i = 0; i < rows; i++)
                {
                    float top = Mathf.Min(EdgeHeight(from, to, rows, i), EdgeHeight(from, to, rows, i + 1));
                    int row = up == WorldKit.Side.North || up == WorldKit.Side.East ? i : rows - 1 - i;
                    Vector3 c = alongZ ? new Vector3(r.x + r.width / 2f, 0f, r.y + row + 0.5f) : new Vector3(r.x + row + 0.5f, 0f, r.y + r.height / 2f);
                    Vector3 s = alongZ ? new Vector3(r.width, 0f, 1f) : new Vector3(1f, 0f, r.height);
                    WorldKit.Prop("Fill", ramp, new Vector3(c.x, (Bottom + top) / 2f, c.z), new Vector3(s.x, top - Bottom, s.z), retain);
                }
                // The slab: one sloping box from the low edge to the high edge, its top on the line between them.
                float run = rows, rise = Mathf.Abs(to - from);
                float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
                float length = Mathf.Sqrt(run * run + rise * rise);
                var mid = new Vector3(r.x + r.width / 2f, (from + to) / 2f, r.y + r.height / 2f);
                Quaternion tilt;
                switch (to >= from ? up : Opposite(up))
                {
                    case WorldKit.Side.North: tilt = Quaternion.Euler(-angle, 0f, 0f); break;
                    case WorldKit.Side.South: tilt = Quaternion.Euler(angle, 0f, 0f); break;
                    case WorldKit.Side.East: tilt = Quaternion.Euler(0f, 0f, angle); break;
                    default: tilt = Quaternion.Euler(0f, 0f, -angle); break;
                }
                const float thick = 0.3f;
                GameObject slab = WorldKit.Box("Slope", ramp, mid - (tilt * Vector3.up) * (thick / 2f),
                    alongZ ? new Vector3(r.width, thick, length) : new Vector3(length, thick, r.height), surface);
                slab.transform.rotation = tilt;
            }
        }

        private void BuildDecks(Transform group)
        {
            var done = new bool[_area.width, _area.height];
            for (int lz = 0; lz < _area.height; lz++)
                for (int lx = 0; lx < _area.width; lx++)
                {
                    if (done[lx, lz] || _kind[lx, lz] != Kind.Deck) continue;
                    float h = _height[lx, lz];
                    bool Same(int x, int z) => !done[x, z] && _kind[x, z] == Kind.Deck && Mathf.Approximately(_height[x, z], h);
                    int w = 1;
                    while (lx + w < _area.width && Same(lx + w, lz)) w++;
                    int d = 1;
                    while (lz + d < _area.height)
                    {
                        bool row = true;
                        for (int i = 0; i < w && row; i++) row = Same(lx + i, lz + d);
                        if (!row) break;
                        d++;
                    }
                    for (int j = 0; j < d; j++)
                        for (int i = 0; i < w; i++)
                            done[lx + i, lz + j] = true;
                    WorldKit.Box("Deck", group, new Vector3(_area.x + lx + w / 2f, h - 0.3f, _area.y + lz + d / 2f), new Vector3(w, 0.6f, d), _surface[lx, lz]);
                }
        }

        /// <summary>
        /// A MapWall on every grid line where the ground on one side is more than a step higher than
        /// on the other (the upper side gets a kerb, or a parapet where the drop is a storey or
        /// more), and a parapet where high ground falls away into nothing (a gorge, a cliff).
        /// Consecutive pieces of the same height along a line are merged.
        /// </summary>
        private void BuildEdges(Transform group, Material parapet)
        {
            // East-west lines (z = const) between rows lz-1 and lz, then north-south lines.
            for (int lz = 1; lz < _area.height; lz++)
                EdgeRun(group, parapet, true, lz, _area.width, i => (i, lz - 1), i => (i, lz));
            for (int lx = 1; lx < _area.width; lx++)
                EdgeRun(group, parapet, false, lx, _area.height, i => (lx - 1, i), i => (lx, i));
        }

        private void EdgeRun(Transform group, Material parapet, bool alongX, int line, int length,
            System.Func<int, (int, int)> a, System.Func<int, (int, int)> b)
        {
            int start = -1;
            float runTop = 0f, runHeight = 0f;
            for (int i = 0; i <= length; i++)
            {
                float top = 0f, height = 0f;
                bool wall = i < length && Edge(a(i), b(i), out top, out height);
                bool continues = wall && start >= 0 && Mathf.Approximately(top, runTop) && Mathf.Approximately(height, runHeight);
                if (start >= 0 && !continues)
                {
                    EdgeWall(group, parapet, alongX, line, start, i, runTop, runHeight);
                    start = -1;
                }
                if (wall && start < 0)
                {
                    start = i;
                    runTop = top;
                    runHeight = height;
                }
            }
        }

        /// <summary>Whether the edge between two tiles needs a wall, and if so the height of its upper side and how tall to draw it.</summary>
        private bool Edge((int x, int z) a, (int x, int z) b, out float top, out float height)
        {
            top = height = 0f;
            Kind ka = _kind[a.x, a.z], kb = _kind[b.x, b.z];
            if (ka == Kind.None && kb == Kind.None) return false;
            if (ka == Kind.None || kb == Kind.None)
            {
                // Ground falling into nothing: a parapet, but only where the fall is high (the
                // quay's edge stays open to the water).
                top = ka == Kind.None ? _height[b.x, b.z] : _height[a.x, a.z];
                if (top < 2.5f) return false;
                height = 1f;
                return true;
            }
            float ha = _height[a.x, a.z], hb = _height[b.x, b.z];
            float drop = Mathf.Abs(ha - hb);
            if (drop <= MaxStep + 0.001f) return false;
            top = Mathf.Max(ha, hb);
            height = drop >= 2.5f ? 1f : 0.35f;
            return true;
        }

        private void EdgeWall(Transform group, Material parapet, bool alongX, int line, int from, int to, float top, float height)
        {
            float a = (alongX ? _area.x : _area.y) + from, b = (alongX ? _area.x : _area.y) + to;
            float l = (alongX ? _area.y : _area.x) + line;
            Vector3 c = alongX ? new Vector3((a + b) / 2f, top + height / 2f - 0.05f, l) : new Vector3(l, top + height / 2f - 0.05f, (a + b) / 2f);
            Vector3 s = alongX ? new Vector3(b - a + 0.2f, height + 0.1f, 0.2f) : new Vector3(0.2f, height + 0.1f, b - a + 0.2f);
            WorldKit.Box(height >= 1f ? "Parapet" : "Kerb", group, c, s, parapet).AddComponent<MapWall>();
        }

        // ------------------------------------------------------------------ helpers

        private static void Block(Transform group, string name, int x, int z, int w, int d, float h, Material surface, Material retain)
        {
            const float cap = 0.15f;
            Vector3 c = new Vector3(x + w / 2f, 0f, z + d / 2f);
            if (h - cap > Bottom)
                WorldKit.Prop(name + " body", group, new Vector3(c.x, (Bottom + h - cap) / 2f, c.z), new Vector3(w, h - cap - Bottom, d), retain);
            WorldKit.Box(name, group, new Vector3(c.x, h - cap / 2f, c.z), new Vector3(w, cap, d), surface);
        }

        private void Set(RectInt r, System.Func<int, int, float> height, Kind kind, Material surface)
        {
            for (int z = r.y; z < r.y + r.height; z++)
                for (int x = r.x; x < r.x + r.width; x++)
                {
                    if (!_area.Contains(new Vector2Int(x, z))) continue;
                    int lx = x - _area.x, lz = z - _area.y;
                    _height[lx, lz] = height(x, z);
                    _kind[lx, lz] = kind;
                    _surface[lx, lz] = surface;
                }
        }

        private static int Rows(RectInt r, WorldKit.Side up) => up == WorldKit.Side.North || up == WorldKit.Side.South ? r.height : r.width;

        /// <summary>The row of a tile counted from the low end of a stair or ramp.</summary>
        private static int Row(RectInt r, WorldKit.Side up, int x, int z)
        {
            switch (up)
            {
                case WorldKit.Side.North: return z - r.y;
                case WorldKit.Side.South: return r.y + r.height - 1 - z;
                case WorldKit.Side.East: return x - r.x;
                default: return r.x + r.width - 1 - x;
            }
        }

        private static float EdgeHeight(float from, float to, int rows, int edge) => from + (to - from) * edge / rows;

        private static WorldKit.Side Opposite(WorldKit.Side s) =>
            s == WorldKit.Side.North ? WorldKit.Side.South : s == WorldKit.Side.South ? WorldKit.Side.North : s == WorldKit.Side.East ? WorldKit.Side.West : WorldKit.Side.East;
    }

    public sealed partial class WorldKit
    {
        /// <summary>
        /// Starts a region for a <see cref="Terraces"/>: the same as <see cref="BeginRegion"/>, but with
        /// no flat ground under everything, since the terrain says where the ground is.
        /// </summary>
        public static MapArea BeginTerrainRegion(string exportName, string title, Vector2Int origin, Vector2Int size)
        {
            MapArea area = BeginRegion(exportName, title, origin, size, null);
            Transform ground = area.transform.Find("Ground");
            if (ground != null) Object.DestroyImmediate(ground.gameObject);
            return area;
        }

        /// <summary>
        /// What holds a bridge deck up over a void: piers and arches under the deck over
        /// <paramref name="deck"/> (absolute tiles) at <paramref name="height"/>, crossing along x or
        /// z, down to <paramref name="foot"/>, with <paramref name="arches"/> arches on each face.
        /// Scenery only (the deck is the terrain's).
        /// </summary>
        public static Transform Viaduct(Transform parent, string name, RectInt deck, float height, bool alongX, float foot, int arches, Material stone)
        {
            Transform group = Group(parent, name);
            float length = alongX ? deck.width : deck.height;
            float span = length / arches;
            foreach (float side in alongX ? new float[] { deck.y + 0.25f, deck.yMax - 0.25f } : new float[] { deck.x + 0.25f, deck.xMax - 0.25f })
            {
                for (int i = 0; i < arches; i++)
                {
                    float a = (alongX ? deck.x : deck.y) + i * span, b = a + span;
                    Vector3 pa = alongX ? new Vector3(a, 0f, side) : new Vector3(side, 0f, a);
                    Vector3 pb = alongX ? new Vector3(b, 0f, side) : new Vector3(side, 0f, b);
                    float crown = Mathf.Min(span * 0.5f, (height - foot) * 0.6f);
                    float spring = height - 0.6f - crown - 0.4f;
                    Arch(group, pa, pb, spring, crown, height - 0.6f, 0.5f, stone, 9);
                }
            }
            // Piers between the arches, the full width of the deck, down to the foot.
            for (int i = 0; i <= arches; i++)
            {
                float at = (alongX ? deck.x : deck.y) + i * span;
                float top = height - 0.6f - Mathf.Min(span * 0.5f, (height - foot) * 0.6f) - 0.4f;
                Vector3 c = alongX ? new Vector3(at, (foot + top) / 2f, deck.center.y) : new Vector3(deck.center.x, (foot + top) / 2f, at);
                Vector3 s = alongX ? new Vector3(1.4f, top - foot, deck.height + 0.6f) : new Vector3(deck.width + 0.6f, top - foot, 1.4f);
                if (top > foot) Prop("Pier", group, c, s, stone);
            }
            return group;
        }

        /// <summary>
        /// A stream along <paramref name="path"/> (points on its surface, any heights): a ribbon of
        /// water between each pair of points, a sheet of falling water wherever the path drops more
        /// than it runs, with a pool of foam at its foot. Scenery only: it lies in a void or under a
        /// MapBlocker, so nobody walks in it.
        /// </summary>
        public static Transform Stream(Transform parent, string name, IList<Vector3> path, float width, Material water, Material foam)
        {
            Transform group = Group(parent, name);
            for (int i = 0; i + 1 < path.Count; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
                float drop = a.y - b.y;
                if (Mathf.Abs(drop) > flat.magnitude)
                {
                    // Falls: a sheet from the lip down to the pool, and foam where it lands.
                    Vector3 lip = a, foot = new Vector3(a.x, b.y, a.z);
                    GameObject sheet = Prop("Falls", group, (lip + foot) / 2f, new Vector3(width * 0.9f, Mathf.Abs(drop), 0.25f), water);
                    if (flat.sqrMagnitude > 0.001f) sheet.transform.rotation = Quaternion.LookRotation(flat);
                    Cylinder("Foam", group, foot + Vector3.up * 0.05f, new Vector3(width * 1.3f, 0.05f, width * 1.3f), foam, collider: false);
                    if (flat.sqrMagnitude > 0.001f)
                        Beam("Water", group, foot + Vector3.up * 0.02f, b + Vector3.up * 0.02f, width, 0.06f, water);
                }
                else Beam("Water", group, a, b, width, 0.06f, water);
            }
            return group;
        }
    }
}
