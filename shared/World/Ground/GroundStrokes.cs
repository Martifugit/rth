using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Rth.Shared.World.Data;
using Rth.Shared.World.Layout;

namespace Rth.Shared.World.Ground
{
    /// <summary>
    /// Paths and rivers redrawn over a set of regions' painted ground (`tools/world strokes`): each
    /// a line through a few points, smoothed and wound gently either side so it is neither straight
    /// nor zigzag, rasterised to tiles. The old line's ground near it is cleared first and filled
    /// with the ground round it; a river's bed and banks are lowered to a level falling along it.
    /// Swaps change one ground for others near water (mud at a river mouth to reeds and sand).
    /// The result is ordinary tile data, edited by hand afterwards like any other.
    /// </summary>
    public sealed class StrokePlan
    {
        /// <summary>The area (world/areas.json) whose regions it draws over; or <see cref="Regions"/>.</summary>
        public string? Area;
        public List<string> Regions = new List<string>();
        public string Note = "";
        /// <summary>Takes generated regions of the plan over first (their "generated" line goes, their scenes to the area's folder), as hand work in the Editor does.</summary>
        public bool TakeOver;
        public List<Stroke> Strokes = new List<Stroke>();
        public List<GroundSwap> Swaps = new List<GroundSwap>();
        /// <summary>Areas of ground drawn from an outline (an island, a hill, a clearing), before the strokes.</summary>
        public List<GroundShape> Shapes = new List<GroundShape>();

        public static StrokePlan Parse(string json) => JsonConvert.DeserializeObject<StrokePlan>(json) ?? new StrokePlan();
    }

    public sealed class Stroke
    {
        public string Id = "";
        /// <summary>The ground drawn (a ground id of continent.json); water grounds make a river.</summary>
        public string Ground = "path";
        /// <summary>The line, in tiles; a river runs from its first point to its last (downstream).</summary>
        public List<float[]> Points = new List<float[]>();
        /// <summary>Full width in tiles at the first and last point.</summary>
        public float[] Width = { 2f, 2f };
        /// <summary>How far, in tiles, the line winds either side of the smoothed points.</summary>
        public float Wind = 1.5f;
        /// <summary>The length, in tiles, of its long bends (smaller ones ride on them).</summary>
        public float Wave = 40f;
        /// <summary>"meander" (bends side to side in turn, as a river does) or "drift" (easy random bends, as a path does); none: by the ground.</summary>
        public string? Bends;
        /// <summary>How much the width varies along it (0..1).</summary>
        public float Ragged = 0.2f;
        public int Seed = 1;
        /// <summary>Grounds of the old line to clear within <see cref="EraseReach"/> tiles of the points' line.</summary>
        public List<string> Erase = new List<string>();
        public float EraseReach = 5f;
        /// <summary>Paths: a ragged fringe of this ground along the edges.</summary>
        public string? Edge;
        public float EdgeShare = 0.35f;
        /// <summary>Grounds the line never paints over (a town's paving it runs into).</summary>
        public List<string> Keep = new List<string>();
        /// <summary>Paths: drawn where they cross water.</summary>
        public string Bridge = "bridge";
        /// <summary>Rivers: patches of other grounds along the banks.</summary>
        public List<Scatter> Banks = new List<Scatter>();
        /// <summary>Rivers: a stretch falling more than this (tiles a tile) becomes one fall with level water above and below; 0: none.</summary>
        public float Fall;
        /// <summary>Rivers: how much wider (half width, tiles) the plunge pool below a fall is.</summary>
        public float Pool = 1.5f;
        /// <summary>Rivers: how far the banks reach from the water, in tiles.</summary>
        public float BankReach = 1.6f;
        /// <summary>
        /// Paths: graded to climb no more than this (tiles a tile) along the line, cutting into rises
        /// and built up over dips, with the land easing back over <see cref="GradeBank"/> tiles either
        /// side; its ends stay at the land's height. A climb steeper than this needs switchbacks (more
        /// points). 0: the path lies on the land as it is.
        /// </summary>
        public float Grade;
        public float GradeBank = 2.5f;
    }

    /// <summary>Patches of a ground: about <see cref="Share"/> of the tiles offered, in blobs about <see cref="Scale"/> tiles across.</summary>
    public sealed class Scatter
    {
        public string Ground = "";
        public float Share = 0.3f;
        public float Scale = 6f;
        /// <summary>Only within [x0, y0, x1, y1) (none: anywhere); read by a water shape's shore.</summary>
        public int[]? Rect;

        public bool Holds(int x, int y) => Rect == null || x >= Rect[0] && y >= Rect[1] && x < Rect[2] && y < Rect[3];
    }

    /// <summary>
    /// An area of ground from a closed outline, smoothed and roughened: an island in a lake, a hill,
    /// a clearing. Optionally clears old ground in a rectangle to water first. Its land rises from
    /// the waterline to a <see cref="Peak"/>: steep near the peak, gentle far from it, so the near
    /// side stands as cliffs and the far side shelves into beaches.
    /// </summary>
    public sealed class GroundShape
    {
        public string Id = "";
        /// <summary>The outline, in tiles, closed (the last point joins the first).</summary>
        public List<float[]> Points = new List<float[]>();
        /// <summary>How far, in tiles, the edge wanders from the smoothed outline.</summary>
        public float Ragged = 0.8f;
        public int Seed = 1;
        public string Ground = "grass";
        /// <summary>Patches of other grounds inside.</summary>
        public List<Scatter> Patches = new List<Scatter>();
        /// <summary>The highest point, and how high it stands above the water (tiles).</summary>
        public float[]? Peak;
        public float Height;
        /// <summary>How far from the peak the land keeps most of its height (tiles).</summary>
        public float Spread = 12f;
        /// <summary>A level top round the peak, this many tiles across (a site kept for a building).</summary>
        public float Plateau;
        /// <summary>How gently the edge rises far from the peak: the width of the shelf there, in tiles.</summary>
        public float Gentle = 4f;
        /// <summary>The way (x, y) the cliffs face: only the edge on that side of the peak stands steep. None: steep near the peak all round.</summary>
        public float[]? CliffSide;
        /// <summary>Low ground along the edge (a beach), and patches of others in it.</summary>
        public string? Beach;
        public List<Scatter> BeachPatches = new List<Scatter>();
        public float BeachWidth = 1.5f;
        /// <summary>Tiles rising more than <see cref="RockRise"/> across become this ground (rock faces).</summary>
        public string? Rock;
        public float RockRise = 0.8f;
        /// <summary>Clears these grounds in this rectangle [x0, y0, x1, y1) to <see cref="ClearTo"/> first, its corners at the water's level.</summary>
        public int[]? Clear;
        public List<string> ClearGrounds = new List<string>();
        public string ClearTo = "lake";
        /// <summary>Land: only the ground is painted (a town's square, a yard); the heights stay as they are.</summary>
        public bool KeepHeights;
        /// <summary>
        /// Only heights: the land inside is raised by this much (lowered if negative) on top of what is
        /// there, easing in over <see cref="Gentle"/> tiles from the edge, or abruptly on the
        /// <see cref="CliffSide"/> of the <see cref="Peak"/> (a crag's face); the ground is left as it
        /// is, but for <see cref="Rock"/> where it now rises steeply (a crown on a hill, a knoll, a crag).
        /// </summary>
        public float Raise;

        // A body of water instead of land (a mere, a pond, a bay): "water": true.

        /// <summary>The outline is water in <see cref="Ground"/> (a lake), its surface level at <see cref="Level"/> (or the lowest water near it).</summary>
        public bool Water;
        public float? Level;
        /// <summary>Water: the old water of these grounds outside the new outline, within <see cref="DrainReach"/> tiles of it, becomes land.</summary>
        public List<string> Drain = new List<string>();
        /// <summary>Water: grounds of the old shore (a sand rim) within <see cref="DrainReach"/>, given the land round them (<see cref="Shore"/> may paint some back).</summary>
        public List<string> DrainGrounds = new List<string>();
        public float DrainReach = 12f;
        /// <summary>Water: how far, in tiles, the land eases down to the new shore.</summary>
        public float ShoreReach = 3f;
        /// <summary>Water: patches along the new shore (reeds, a little beach), within <see cref="BeachWidth"/> tiles of the water; each may be held to a rect.</summary>
        public List<Scatter> Shore = new List<Scatter>();
    }

    public sealed class GroundSwap
    {
        public List<string> From = new List<string>();
        /// <summary>Within this many tiles of water (0: anywhere).</summary>
        public int NearWater = 3;
        /// <summary>Only tiles with no water within this many tiles (0: no such limit): a bank left behind by a river that moved.</summary>
        public int AwayFromWater;
        /// <summary>Only tiles within <see cref="SteepReach"/> of a tile rising more than this across (tiles; 0: no such limit): a rock band along a bluff.</summary>
        public float Steep;
        public int SteepReach = 1;
        public List<Scatter> To = new List<Scatter>();
        /// <summary>Where no patch of <see cref="To"/> falls: a ground, or "surround" for the commonest ground round the tile.</summary>
        public string Otherwise = "grass";
        /// <summary>Limit to tiles [x0, y0, x1, y1); none: everywhere in the regions.</summary>
        public int[]? Rect;
        /// <summary>Only patches of the <see cref="From"/> grounds (joined edge to edge) smaller than this many tiles: scraps of field, islets in a river (0: any size).</summary>
        public int Smaller;
        public int Seed = 7;
    }

    public static class GroundStrokes
    {
        /// <summary>Applies a plan to the regions (edited in place) and returns what it did, a line each.</summary>
        public static List<string> Apply(IReadOnlyList<RegionData> regions, ContinentData data, StrokePlan plan)
        {
            var g = new Grid(regions, data);
            var log = new List<string>();
            foreach (GroundShape sh in plan.Shapes) log.Add(Fill(g, sh));
            foreach (Stroke s in plan.Strokes) log.Add(Draw(g, s));
            foreach (GroundSwap w in plan.Swaps) log.Add(Swap(g, w));
            g.WriteTerrain();
            return log;
        }

        // ------------------------------------------------------------------ the line

        /// <summary>The smoothed, wound line: points a tile or less apart.</summary>
        public static List<float[]> Line(Stroke s)
        {
            var pts = s.Points;
            var smooth = new List<float[]>();
            if (pts.Count < 2) return smooth;
            // Catmull-Rom through the points, a few samples a tile.
            for (int k = 0; k + 1 < pts.Count; k++)
            {
                float[] p0 = pts[Math.Max(0, k - 1)], p1 = pts[k], p2 = pts[k + 1], p3 = pts[Math.Min(pts.Count - 1, k + 2)];
                int n = Math.Max(2, (int)Math.Ceiling(Dist(p1, p2) * 4));
                for (int j = 0; j < n; j++)
                {
                    float t = j / (float)n, t2 = t * t, t3 = t2 * t;
                    float x = 0.5f * (2 * p1[0] + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2 + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3);
                    float y = 0.5f * (2 * p1[1] + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2 + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3);
                    smooth.Add(new[] { x, y });
                }
            }
            smooth.Add(pts[pts.Count - 1]);
            float total = 0f;
            var along = new float[smooth.Count];
            for (int k = 1; k < smooth.Count; k++) along[k] = total += Dist(smooth[k - 1], smooth[k]);
            // Wound either side, none at the ends, which stay put. A river meanders: bends to one side
            // and the other in turn, each a little longer or deeper than the last. A path drifts: long
            // easy bends with smaller ones on them.
            bool meander = s.Bends == "meander";
            var wound = new List<float[]>();
            float last = -1f, phase = LandNoise.Hash01(s.Seed, 3, 5) * 6.2832f, prev = 0f;
            for (int k = 0; k < smooth.Count; k++)
            {
                phase += (along[k] - prev) / (s.Wave * (1f + 0.3f * LandNoise.Value(s.Seed + 4, along[k] / s.Wave, 0.2f))) * 6.2832f;
                prev = along[k];
                if (k > 0 && k < smooth.Count - 1 && along[k] - last < 0.5f) continue;
                last = along[k];
                float[] a = smooth[Math.Max(0, k - 1)], b = smooth[Math.Min(smooth.Count - 1, k + 1)];
                float dx = b[0] - a[0], dy = b[1] - a[1], len = (float)Math.Sqrt(dx * dx + dy * dy);
                float ends = LandNoise.Smooth(Math.Min(along[k], total - along[k]) / 10f);
                float wind = s.Wind * ends * (meander
                    ? (float)Math.Sin(phase) * (0.7f + 0.3f * LandNoise.Value(s.Seed + 5, along[k] / (s.Wave * 1.7f), 0.7f)) +
                      0.15f * LandNoise.Value(s.Seed + 1, along[k] / (s.Wave / 3.3f), 0.5f)
                    : 0.85f * LandNoise.Value(s.Seed, along[k] / s.Wave, 0.5f) + 0.3f * LandNoise.Value(s.Seed + 1, along[k] / (s.Wave / 3.3f), 0.5f));
                float x = smooth[k][0], y = smooth[k][1];
                if (len > 0) { x += -dy / len * wind; y += dx / len * wind; }
                wound.Add(new[] { x, y });
            }
            return wound;
        }

        private static string Draw(Grid g, Stroke s)
        {
            if (!g.Has(s.Ground)) return $"{s.Id}: no ground '{s.Ground}' in continent.json.";
            s.Bends ??= g.IsWater(s.Ground) ? "meander" : "drift";
            List<float[]> line = Line(s);
            if (line.Count < 2) return $"{s.Id}: needs two points or more.";
            bool river = g.IsWater(s.Ground);
            float total = 0f;
            var along = new float[line.Count];
            for (int k = 1; k < line.Count; k++) along[k] = total += Dist(line[k - 1], line[k]);
            float maxHalf = Math.Max(s.Width[0], s.Width[s.Width.Length - 1]) / 2f * (1f + s.Ragged) + s.BankReach + 2f;

            // Every tile near the line: its distance and how far along.
            var near = new Dictionary<(int, int), (float d, float at)>();
            for (int k = 0; k + 1 < line.Count; k++)
            {
                float[] a = line[k], b = line[k + 1];
                int x0 = (int)Math.Floor(Math.Min(a[0], b[0]) - maxHalf), x1 = (int)Math.Ceiling(Math.Max(a[0], b[0]) + maxHalf);
                int y0 = (int)Math.Floor(Math.Min(a[1], b[1]) - maxHalf), y1 = (int)Math.Ceiling(Math.Max(a[1], b[1]) + maxHalf);
                float dx = b[0] - a[0], dy = b[1] - a[1], len2 = dx * dx + dy * dy;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        float t = len2 > 0 ? Math.Max(0f, Math.Min(1f, ((px - a[0]) * dx + (py - a[1]) * dy) / len2)) : 0f;
                        float d = (float)Math.Sqrt(Sq(a[0] + t * dx - px) + Sq(a[1] + t * dy - py));
                        if (!near.TryGetValue((x, y), out var have) || d < have.d) near[(x, y)] = (d, along[k] + t * (float)Math.Sqrt(len2));
                    }
            }
            // A river's level along it, from the old water near its line (before anything changes),
            // with each steep stretch gathered into one fall.
            var erase = new HashSet<string>(s.Erase);
            float[] levels = Array.Empty<float>();
            var falls = new List<float>();
            if (river)
            {
                var oldAll = g.TilesNear(s.Points, s.EraseReach + maxHalf)
                    .Where(t => g.GroundAt(t.x, t.y) is string id && g.IsWater(id) && (id == s.Ground || erase.Contains(id)) &&
                                DistToPolyline(s.Points, t.x + 0.5f, t.y + 0.5f) <= s.EraseReach + maxHalf)
                    .ToList();
                levels = g.Levels(line, oldAll);
                if (s.Fall > 0f) falls = Falls(levels, along, s.Fall);
            }
            // Below a fall the river widens into its plunge pool.
            float Pool(float at)
            {
                float extra = 0f;
                foreach (float f in falls)
                    extra = Math.Max(extra, s.Pool * Math.Max(0f, 1f - Math.Abs(at - (f + 2f)) / 3.5f));
                return extra;
            }
            float HalfAt(float at) =>
                (s.Width[0] + (s.Width[s.Width.Length - 1] - s.Width[0]) * (total > 0 ? at / total : 0f)) / 2f *
                (1f + s.Ragged * LandNoise.Value(s.Seed + 2, at / 9f, 0.3f)) + Pool(at);
            var core = new HashSet<(int, int)>(near.Where(e => e.Value.d <= HalfAt(e.Value.at)).Select(e => e.Key));

            // The old line: its grounds near the points' straight line, cleared and filled from round about.
            var cleared = new HashSet<(int, int)>();
            if (erase.Count > 0)
                foreach ((int x, int y) t in g.TilesNear(s.Points, s.EraseReach + maxHalf))
                    if (!core.Contains(t) && DistToPolyline(s.Points, t.x + 0.5f, t.y + 0.5f) <= s.EraseReach &&
                        g.GroundAt(t.x, t.y) is string id && erase.Contains(id))
                        cleared.Add(t);
            var oldWater = river ? cleared.Where(t => g.IsWater(g.GroundAt(t.Item1, t.Item2))).ToList() : new List<(int, int)>();
            // A cleared bridge goes back to the water under it; the rest fills from the land round it.
            foreach ((int x, int y) t in cleared.Where(t => g.GroundAt(t.Item1, t.Item2) == s.Bridge).ToList())
                if (g.WaterNear(t.x, t.y) is string water) { g.Set(t.x, t.y, water); cleared.Remove(t); }
            // So does an old road laid across a river (water on opposite sides of it), from the banks inwards,
            // whether it is cleared or lies under the new line (which then crosses on a bridge).
            var across = new HashSet<(int, int)>(cleared);
            across.UnionWith(core.Where(t => g.GroundAt(t.Item1, t.Item2) is string id && erase.Contains(id)));
            for (bool more = true; more;)
            {
                more = false;
                foreach ((int x, int y) t in across.ToList())
                {
                    bool Wet(int dx, int dy) => g.IsWater(g.GroundAt(t.x + dx, t.y + dy));
                    if (!(Wet(1, 0) && Wet(-1, 0) || Wet(0, 1) && Wet(0, -1) || Wet(1, 1) && Wet(-1, -1) || Wet(1, -1) && Wet(-1, 1))) continue;
                    if (g.WaterNear(t.x, t.y) is string water) { g.Set(t.x, t.y, water); cleared.Remove(t); across.Remove(t); more = true; }
                }
            }
            g.Fill(cleared, erase);

            var keep = new HashSet<string>(s.Keep);
            int bridges = 0, drawn = 0;
            foreach ((int x, int y) t in core)
            {
                string? here = g.GroundAt(t.x, t.y);
                if (here == null) continue;
                if (keep.Contains(here)) continue;
                if (!river && g.IsWater(here)) { if (g.Set(t.x, t.y, s.Bridge)) bridges++; continue; }
                if (river && g.IsWater(here) && here != s.Ground) continue; // the lake it runs into
                if (g.Set(t.x, t.y, s.Ground)) drawn++;
            }
            if (!river && s.Edge != null && g.Has(s.Edge))
                foreach (var e in near)
                {
                    float hw = HalfAt(e.Value.at);
                    if (core.Contains(e.Key) || e.Value.d > hw + 1f) continue;
                    string? here = g.GroundAt(e.Key.Item1, e.Key.Item2);
                    if (here == null || g.IsWater(here) || !g.IsNatural(here)) continue;
                    if (LandNoise.Hash01(s.Seed + 3, e.Key.Item1, e.Key.Item2) < s.EdgeShare * (1f - (e.Value.d - hw))) g.Set(e.Key.Item1, e.Key.Item2, s.Edge);
                }
            if (river)
            {
                for (int k = 0; k < s.Banks.Count; k++)
                {
                    Scatter b = s.Banks[k];
                    if (!g.Has(b.Ground)) continue;
                    foreach (var e in near)
                    {
                        float hw = HalfAt(e.Value.at);
                        if (core.Contains(e.Key) || e.Value.d > hw + s.BankReach) continue;
                        string? here = g.GroundAt(e.Key.Item1, e.Key.Item2);
                        if (here == null || g.IsWater(here) || !g.IsNatural(here)) continue;
                        if (Patch(s.Seed + 20 + k, e.Key.Item1, e.Key.Item2, b)) g.Set(e.Key.Item1, e.Key.Item2, b.Ground);
                    }
                }
                g.Carve(core.Where(t => g.GroundAt(t.Item1, t.Item2) == s.Ground).ToList(), oldWater, line, levels);
            }
            string graded = !river && s.Grade > 0f ? GradeRoad(g, s, line, along, total, HalfAt, core, near) : "";
            return $"{s.Id}: {drawn} tiles of {s.Ground} along {total:0} tiles, {cleared.Count} old tiles cleared" + graded +
                   string.Concat(falls.Select(f => $", a fall {f:0} tiles along")) +
                   (bridges > 0 ? $", {bridges} tiles of {s.Bridge} over water" : "") + ".";
        }

        /// <summary>
        /// A road's own gradient: the land's heights along it, smoothed, held to the stroke's grade
        /// (ends fixed) with a level landing at each sharp turn (a switchback's hairpin), written over
        /// the road tile by tile and eased into the land beside it. Corners touching water keep their
        /// height (a bridge leaves the river under it alone).
        /// </summary>
        private static string GradeRoad(Grid g, Stroke s, List<float[]> line, float[] along, float total,
                                        Func<float, float> halfAt, HashSet<(int, int)> core, Dictionary<(int, int), (float d, float at)> near)
        {
            int n = line.Count;
            var raw = new float[n];
            for (int k = 0; k < n; k++)
                raw[k] = g.Corner((int)Math.Round(line[k][0]), (int)Math.Round(line[k][1])) ?? 0f;
            // Smoothed over about four tiles either way.
            var p = new float[n];
            for (int k = 0; k < n; k++)
            {
                float sum = 0f, w = 0f;
                for (int j = 0; j < n; j++)
                {
                    float d = Math.Abs(along[j] - along[k]);
                    if (d > 4f) continue;
                    float wt = 1f - d / 4.5f;
                    sum += raw[j] * wt; w += wt;
                }
                p[k] = sum / w;
            }
            p[0] = raw[0]; p[n - 1] = raw[n - 1];
            // Landings: within four tiles of a control point where the road turns by more than 100 degrees.
            var landing = new bool[n];
            for (int c = 1; c + 1 < s.Points.Count; c++)
            {
                float[] a = s.Points[c - 1], b = s.Points[c], d = s.Points[c + 1];
                float ux = b[0] - a[0], uy = b[1] - a[1], vx = d[0] - b[0], vy = d[1] - b[1];
                float cos = (ux * vx + uy * vy) / Math.Max(1e-3f, (float)Math.Sqrt((ux * ux + uy * uy) * (vx * vx + vy * vy)));
                if (cos > -0.17f) continue;
                int at = 0;
                float bd = float.MaxValue;
                for (int k = 0; k < n; k++) { float d2 = Sq(line[k][0] - b[0]) + Sq(line[k][1] - b[1]); if (d2 < bd) { bd = d2; at = k; } }
                for (int k = 0; k < n; k++) if (Math.Abs(along[k] - along[at]) <= 4f) landing[k] = true;
            }
            // Held to the grade (level on landings): forward from the start, back from the end, then the ends restored.
            float m = s.Grade;
            for (int pass = 0; pass < 4; pass++)
            {
                for (int k = 1; k < n; k++) { float lim = landing[k] && landing[k - 1] ? 0f : m * (along[k] - along[k - 1]); p[k] = Math.Max(p[k - 1] - lim, Math.Min(p[k - 1] + lim, p[k])); }
                p[n - 1] = raw[n - 1];
                for (int k = n - 2; k >= 0; k--) { float lim = landing[k] && landing[k + 1] ? 0f : m * (along[k + 1] - along[k]); p[k] = Math.Max(p[k + 1] - lim, Math.Min(p[k + 1] + lim, p[k])); }
                p[0] = raw[0];
            }
            float steepest = 0f;
            for (int k = 1; k < n; k++) { float ds = Math.Max(1e-3f, along[k] - along[k - 1]); steepest = Math.Max(steepest, Math.Abs(p[k] - p[k - 1]) / ds); }
            float ProfileAt(float a)
            {
                if (a <= along[0]) return p[0];
                for (int k = 1; k < n; k++)
                    if (a <= along[k]) { float t = (a - along[k - 1]) / Math.Max(1e-4f, along[k] - along[k - 1]); return p[k - 1] + (p[k] - p[k - 1]) * t; }
                return p[n - 1];
            }

            bool TouchesWater(int cx, int cy) =>
                g.IsWater(g.GroundAt(cx - 1, cy - 1)) || g.IsWater(g.GroundAt(cx, cy - 1)) || g.IsWater(g.GroundAt(cx - 1, cy)) || g.IsWater(g.GroundAt(cx, cy));
            // The road: each tile's height from its place along the line, its corners the mean of the road tiles round them.
            var sums = new Dictionary<(int, int), (float sum, int count)>();
            foreach ((int x, int y) t in core)
            {
                if (g.IsWater(g.GroundAt(t.x, t.y)) || !near.TryGetValue(t, out var nr)) continue;
                float target = ProfileAt(nr.at);
                foreach (var c in new[] { (t.x, t.y), (t.x + 1, t.y), (t.x, t.y + 1), (t.x + 1, t.y + 1) })
                    sums[c] = sums.TryGetValue(c, out var v) ? (v.sum + target, v.count + 1) : (target, 1);
            }
            var road = new Dictionary<(int, int), float>();
            foreach (var e in sums)
                if (!TouchesWater(e.Key.Item1, e.Key.Item2)) road[e.Key] = e.Value.sum / e.Value.count;
            // The land beside it eases back from the nearest road corner.
            int bank = (int)Math.Ceiling(s.GradeBank);
            var eased = new Dictionary<(int, int), float>();
            foreach (var r in road)
                for (int j = -bank; j <= bank; j++)
                    for (int i = -bank; i <= bank; i++)
                    {
                        var c = (r.Key.Item1 + i, r.Key.Item2 + j);
                        if (road.ContainsKey(c) || !(g.Corner(c.Item1, c.Item2) is float here) || TouchesWater(c.Item1, c.Item2)) continue;
                        float d = (float)Math.Sqrt(i * i + j * j);
                        if (d > s.GradeBank) continue;
                        float h = r.Value + (here - r.Value) * LandNoise.Smooth(d / s.GradeBank);
                        // The nearest road corner wins.
                        if (!eased.TryGetValue(c, out float had) || d < DistToRoad(c)) eased[c] = h;
                        float DistToRoad((int, int) q) { float best = float.MaxValue; for (int jj = -bank; jj <= bank; jj++) for (int ii = -bank; ii <= bank; ii++) if (road.ContainsKey((q.Item1 + ii, q.Item2 + jj))) best = Math.Min(best, (float)Math.Sqrt(ii * ii + jj * jj)); return best; }
                    }
            foreach (var e in road) g.SetCorner(e.Key.Item1, e.Key.Item2, e.Value);
            foreach (var e in eased) g.SetCorner(e.Key.Item1, e.Key.Item2, e.Value);
            return $", graded ({road.Count} road corners, steepest {steepest:0.00} a tile{(steepest > m + 0.01f ? ": too steep for its length, add switchbacks" : "")})";
        }

        /// <summary>
        /// Gathers each stretch steeper than <paramref name="maxSlope"/> (tiles of fall a tile, gaps
        /// of up to three tiles joining stretches) into one step at its steepest point: level water
        /// above and below, the drop all in one place. Returns how far along each fall is.
        /// </summary>
        private static string Fill(Grid g, GroundShape sh)
        {
            if (sh.Points.Count < 3) return $"{sh.Id}: needs three points or more.";
            // The outline, smoothed (closed Catmull-Rom).
            var outline = new List<float[]>();
            int n = sh.Points.Count;
            for (int k = 0; k < n; k++)
            {
                float[] p0 = sh.Points[(k - 1 + n) % n], p1 = sh.Points[k], p2 = sh.Points[(k + 1) % n], p3 = sh.Points[(k + 2) % n];
                int steps = Math.Max(2, (int)Math.Ceiling(Dist(p1, p2) * 3));
                for (int j = 0; j < steps; j++)
                {
                    float t = j / (float)steps, t2 = t * t, t3 = t2 * t;
                    outline.Add(new[]
                    {
                        0.5f * (2 * p1[0] + (-p0[0] + p2[0]) * t + (2 * p0[0] - 5 * p1[0] + 4 * p2[0] - p3[0]) * t2 + (-p0[0] + 3 * p1[0] - 3 * p2[0] + p3[0]) * t3),
                        0.5f * (2 * p1[1] + (-p0[1] + p2[1]) * t + (2 * p0[1] - 5 * p1[1] + 4 * p2[1] - p3[1]) * t2 + (-p0[1] + 3 * p1[1] - 3 * p2[1] + p3[1]) * t3),
                    });
                }
            }
            var closed = new List<float[]>(outline) { outline[0] };
            // Signed distance to the edge (inside positive), roughened.
            float Edge(float x, float y) =>
                (InsidePolygon(outline, x, y) ? 1f : -1f) * DistToPolyline(closed, x, y) +
                sh.Ragged * (0.7f * LandNoise.Value(sh.Seed, x / 5f, y / 5f) + 0.3f * LandNoise.Value(sh.Seed + 1, x / 2.2f, y / 2.2f));
            int x0 = (int)Math.Floor(outline.Min(p => p[0]) - sh.Ragged - 2), x1 = (int)Math.Ceiling(outline.Max(p => p[0]) + sh.Ragged + 2);
            int y0 = (int)Math.Floor(outline.Min(p => p[1]) - sh.Ragged - 2), y1 = (int)Math.Ceiling(outline.Max(p => p[1]) + sh.Ragged + 2);
            if (sh.Water) return FillWater(g, sh, Edge, x0, y0, x1, y1);
            if (sh.Raise != 0f)
            {
                // How far in from the edge the rise is reached: gentle, or short on the cliff side (a crag's face).
                // Which way the nearest stretch of the outline faces: steep where it faces the cliff side.
                // (By the edge, not the peak, so a long scarp is steep along its whole face.)
                bool clockwise = SignedArea(closed) < 0f;
                float Shoulder(float x, float y)
                {
                    if (sh.CliffSide == null) return Math.Max(1f, sh.Gentle);
                    float best = float.MaxValue, nx = 0f, ny = 0f;
                    for (int k = 0; k + 1 < closed.Count; k++)
                    {
                        float[] a = closed[k], b = closed[k + 1];
                        float dx = b[0] - a[0], dy = b[1] - a[1], len2 = dx * dx + dy * dy;
                        if (len2 < 1e-6f) continue;
                        float t = Math.Max(0f, Math.Min(1f, ((x - a[0]) * dx + (y - a[1]) * dy) / len2));
                        float d = Sq(a[0] + t * dx - x) + Sq(a[1] + t * dy - y);
                        if (d >= best) continue;
                        best = d;
                        float len = (float)Math.Sqrt(len2);
                        // Outward normal: right of the direction for an anticlockwise outline, left for a clockwise one.
                        nx = (clockwise ? -dy : dy) / len; ny = (clockwise ? dx : -dx) / len;
                    }
                    float side = (float)Math.Sqrt(Sq(sh.CliffSide[0]) + Sq(sh.CliffSide[1]));
                    float facing = (nx * sh.CliffSide[0] + ny * sh.CliffSide[1]) / Math.Max(1e-3f, side);
                    return 0.6f + (Math.Max(1f, sh.Gentle) - 0.6f) * (1f - LandNoise.Smooth((facing - 0.35f) / 0.4f));
                }
                var raised = new Dictionary<(int, int), float>();
                for (int cy = y0; cy <= y1; cy++)
                    for (int cx = x0; cx <= x1; cx++)
                    {
                        float d = Edge(cx, cy);
                        if (d <= 0f || !(g.Corner(cx, cy) is float h)) continue;
                        raised[(cx, cy)] = h + sh.Raise * LandNoise.Smooth(Math.Min(1f, d / Shoulder(cx, cy)));
                    }
                foreach (var r in raised) g.SetCorner(r.Key.Item1, r.Key.Item2, r.Value);
                // Rock where it now rises steeply.
                int rocky = 0;
                if (sh.Rock != null && g.Has(sh.Rock))
                    for (int ty = y0; ty < y1; ty++)
                        for (int tx = x0; tx < x1; tx++)
                            if (g.GroundAt(tx, ty) is string id && g.IsNatural(id) && g.RiseOf(tx, ty) > sh.RockRise && g.Set(tx, ty, sh.Rock)) rocky++;
                return $"{sh.Id}: {raised.Count} corners raised by up to {sh.Raise:0.00}, {rocky} tiles of rock.";
            }

            // The water's level round it: the lowest corner of water near it.
            float level = float.NaN;
            int[] look = sh.Clear ?? new[] { x0 - 3, y0 - 3, x1 + 3, y1 + 3 };
            for (int y = look[1]; y < look[3]; y++)
                for (int x = look[0]; x < look[2]; x++)
                    if (g.IsWater(g.GroundAt(x, y)) && g.Corner(x, y) is float h && (float.IsNaN(level) || h < level)) level = h;
            if (float.IsNaN(level)) level = g.Corner((int)outline[0][0], (int)outline[0][1]) ?? 0f;

            int cleared = 0;
            if (sh.Clear != null && g.Has(sh.ClearTo))
            {
                var grounds = new HashSet<string>(sh.ClearGrounds);
                for (int y = sh.Clear[1]; y < sh.Clear[3]; y++)
                    for (int x = sh.Clear[0]; x < sh.Clear[2]; x++)
                        if (g.GroundAt(x, y) is string id && grounds.Contains(id) && g.Set(x, y, sh.ClearTo)) cleared++;
                for (int y = sh.Clear[1]; y <= sh.Clear[3]; y++)
                    for (int x = sh.Clear[0]; x <= sh.Clear[2]; x++)
                        if (g.IsWater(g.GroundAt(x - 1, y - 1)) && g.IsWater(g.GroundAt(x, y - 1)) && g.IsWater(g.GroundAt(x - 1, y)) && g.IsWater(g.GroundAt(x, y)))
                            g.SetCorner(x, y, level);
            }

            // Heights: from just above the water at the edge to the peak, steep near it, gentle far off.
            const float bank = 0.12f;
            float Rise(float x, float y)
            {
                float d = Math.Max(0f, Edge(x, y));
                if (sh.Peak == null || sh.Height <= 0f) return level + bank;
                float dp = (float)Math.Sqrt(Sq(x - sh.Peak[0]) + Sq(y - sh.Peak[1]));
                float top = Math.Max(0f, dp - sh.Plateau / 2f);
                float k = Math.Max(0.12f, 1f - Sq(top / Math.Max(1f, sh.Spread)));
                float shoulder = Math.Max(0.6f, sh.Gentle * dp / Math.Max(1f, sh.Spread));
                if (sh.CliffSide != null && dp > 1e-3f)
                {
                    // Steep only on the side facing the cliff's way; gentle everywhere else.
                    float len = (float)Math.Sqrt(Sq(sh.CliffSide[0]) + Sq(sh.CliffSide[1]));
                    float facing = ((x - sh.Peak[0]) * sh.CliffSide[0] + (y - sh.Peak[1]) * sh.CliffSide[1]) / (dp * Math.Max(1e-3f, len));
                    shoulder = 0.6f + (sh.Gentle - 0.6f) * (1f - LandNoise.Smooth((facing - 0.25f) / 0.5f));
                }
                return level + bank + sh.Height * k * LandNoise.Smooth(d / shoulder);
            }
            var inside = new HashSet<(int, int)>();
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    if (Edge(x + 0.5f, y + 0.5f) > 0f && g.GroundAt(x, y) != null) inside.Add((x, y));
            var corners = new HashSet<(int, int)>();
            foreach ((int x, int y) in inside)
                foreach (var c in new[] { (x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1) }) corners.Add(c);
            if (!sh.KeepHeights)
                foreach ((int cx, int cy) in corners) g.SetCorner(cx, cy, Rise(cx, cy));

            // Ground: rock where it rises steeply, beach along the low edge, patches over the rest.
            int land = 0, rock = 0, beach = 0;
            foreach ((int x, int y) in inside)
            {
                float a = sh.KeepHeights ? 0f : Rise(x, y), b = sh.KeepHeights ? 0f : Rise(x + 1, y), c = sh.KeepHeights ? 0f : Rise(x, y + 1), d = sh.KeepHeights ? 0f : Rise(x + 1, y + 1);
                float high = Math.Max(Math.Max(a, b), Math.Max(c, d)), rise = high - Math.Min(Math.Min(a, b), Math.Min(c, d));
                string ground;
                if (sh.Rock != null && rise > sh.RockRise) { ground = sh.Rock; rock++; }
                else if (sh.Beach != null && Edge(x + 0.5f, y + 0.5f) < sh.BeachWidth && high < level + bank + 0.45f)
                {
                    ground = sh.BeachPatches.FirstOrDefault(p => g.Has(p.Ground) && Patch(sh.Seed + 30 + sh.BeachPatches.IndexOf(p), x, y, p))?.Ground ?? sh.Beach;
                    beach++;
                }
                else ground = sh.Patches.FirstOrDefault(p => g.Has(p.Ground) && Patch(sh.Seed + 10 + sh.Patches.IndexOf(p), x, y, p))?.Ground ?? sh.Ground;
                g.Set(x, y, ground);
                land++;
            }
            return $"{sh.Id}: {land} tiles ({beach} beach, {rock} rock), {cleared} old tiles cleared, water at {level:0.00}.";
        }

        /// <summary>
        /// A body of water from the outline: inside it is water at one level; the old water of the
        /// <see cref="GroundShape.Drain"/> grounds outside it (and an old shore rim left away from the
        /// water) becomes the land round it, its ground rising smoothly from the new shore to the land
        /// that was never touched; then patches along the new shore.
        /// </summary>
        private static string FillWater(Grid g, GroundShape sh, Func<float, float, float> edge, int x0, int y0, int x1, int y1)
        {
            if (!g.Has(sh.Ground) || !g.IsWater(sh.Ground)) return $"{sh.Id}: '{sh.Ground}' is not a water ground.";
            int reach = (int)Math.Ceiling(sh.DrainReach);
            float level = float.NaN;
            if (sh.Level is float fixedLevel) level = fixedLevel;
            else
                for (int y = y0 - 3; y < y1 + 3; y++)
                    for (int x = x0 - 3; x < x1 + 3; x++)
                        if (g.IsWater(g.GroundAt(x, y)) && g.Corner(x, y) is float h && (float.IsNaN(level) || h < level)) level = h;
            var wet = new HashSet<(int, int)>();
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                    if (edge(x + 0.5f, y + 0.5f) > 0f && g.GroundAt(x, y) != null) wet.Add((x, y));
            // No water near (a pond on dry land): a little under the lowest ground it covers.
            if (float.IsNaN(level))
                foreach ((int x, int y) in wet)
                    if (g.Corner(x, y) is float h && (float.IsNaN(level) || h - 0.1f < level)) level = h - 0.1f;
            if (float.IsNaN(level)) level = 0f;
            var drain = new HashSet<string>(sh.Drain);
            var dried = new HashSet<(int, int)>();
            for (int y = y0 - reach; y < y1 + reach; y++)
                for (int x = x0 - reach; x < x1 + reach; x++)
                    if (!wet.Contains((x, y)) && g.GroundAt(x, y) is string id && drain.Contains(id) && edge(x + 0.5f, y + 0.5f) > -sh.DrainReach)
                        dried.Add((x, y));

            foreach ((int x, int y) in wet) g.Set(x, y, sh.Ground);
            var erase = new HashSet<string>(drain) { sh.Ground };
            erase.UnionWith(sh.DrainGrounds);
            g.Fill(dried, erase);
            // The old shore's rim.
            var rim = new HashSet<string>(sh.DrainGrounds);
            var stranded = new HashSet<(int, int)>();
            for (int y = y0 - reach; y < y1 + reach; y++)
                for (int x = x0 - reach; x < x1 + reach; x++)
                    if (g.GroundAt(x, y) is string id && rim.Contains(id) && edge(x + 0.5f, y + 0.5f) > -sh.DrainReach) stranded.Add((x, y));
            g.Fill(stranded, erase);
            dried.UnionWith(stranded);

            // Heights: the water level; the land round what changed relaxed smoothly between it and the land beyond.
            const float bank = 0.12f;
            bool TouchesWater(int cx, int cy) =>
                g.IsWater(g.GroundAt(cx - 1, cy - 1)) || g.IsWater(g.GroundAt(cx, cy - 1)) || g.IsWater(g.GroundAt(cx - 1, cy)) || g.IsWater(g.GroundAt(cx, cy));
            foreach ((int x, int y) in wet)
                foreach (var c in new[] { (x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1) }) g.SetCorner(c.Item1, c.Item2, level);
            int sr = Math.Max(1, (int)Math.Ceiling(sh.ShoreReach));
            var free = new HashSet<(int, int)>();
            foreach ((int x, int y) in wet.Concat(dried))
                for (int j = -sr; j <= sr + 1; j++)
                    for (int i = -sr; i <= sr + 1; i++)
                    {
                        int cx = x + i, cy = y + j;
                        if (!free.Contains((cx, cy)) && !TouchesWater(cx, cy) && g.Corner(cx, cy) != null &&
                            g.GroundAt(cx - 1, cy - 1) != null && g.GroundAt(cx, cy - 1) != null && g.GroundAt(cx - 1, cy) != null && g.GroundAt(cx, cy) != null)
                            free.Add((cx, cy));
                    }
            var h0 = new Dictionary<(int, int), float>();
            foreach ((int cx, int cy) in free)
                foreach (var c in new[] { (cx, cy), (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) })
                    if (!h0.ContainsKey(c) && g.Corner(c.Item1, c.Item2) is float h) h0[c] = h;
            foreach (var c in free) h0[c] = Math.Max(h0[c], level + bank);
            var order = free.OrderBy(c => c.Item2).ThenBy(c => c.Item1).ToList();
            for (int pass = 0; pass < 400; pass++)
                foreach (var c in order)
                {
                    float sum = 0f;
                    int n = 0;
                    foreach (var o in new[] { (c.Item1 + 1, c.Item2), (c.Item1 - 1, c.Item2), (c.Item1, c.Item2 + 1), (c.Item1, c.Item2 - 1) })
                        if (h0.TryGetValue(o, out float v)) { sum += v; n++; }
                    if (n > 0) h0[c] = Math.Max(level + bank, sum / n);
                }
            foreach (var c in order) g.SetCorner(c.Item1, c.Item2, h0[c]);

            // Patches along the shore.
            int bw = Math.Max(1, (int)Math.Ceiling(sh.BeachWidth)), shore = 0;
            for (int y = y0 - reach; y < y1 + reach; y++)
                for (int x = x0 - reach; x < x1 + reach; x++)
                {
                    if (!(g.GroundAt(x, y) is string id) || !g.IsNatural(id) || !g.WaterWithin(x, y, bw)) continue;
                    for (int k = 0; k < sh.Shore.Count; k++)
                    {
                        Scatter s = sh.Shore[k];
                        if (!s.Holds(x, y) || !g.Has(s.Ground) || !Patch(sh.Seed + 40 + k, x, y, s)) continue;
                        if (g.Set(x, y, s.Ground)) shore++;
                        break;
                    }
                }
            return $"{sh.Id}: {wet.Count} tiles of water at {level:0.00}, {dried.Count} tiles dried to land, {free.Count} corners eased, {shore} shore tiles.";
        }

        private static float SignedArea(List<float[]> poly)
        {
            // Shoelace: positive for an anticlockwise outline (x east, y north).
            float a = 0f;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) a += poly[j][0] * poly[i][1] - poly[i][0] * poly[j][1];
            return a / 2f;
        }

        private static bool InsidePolygon(List<float[]> poly, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if (poly[i][1] > y != poly[j][1] > y && x < (poly[j][0] - poly[i][0]) * (y - poly[i][1]) / (poly[j][1] - poly[i][1]) + poly[i][0])
                    inside = !inside;
            return inside;
        }

        /// <summary>The least drop, in tiles, gathered into a fall.</summary>
        public const float MinFall = 0.4f;

        private static List<float> Falls(float[] levels, float[] along, float maxSlope)
        {
            var steps = new List<float>();
            bool Steep(int i) => along[i] - along[i - 1] > 1e-3f && (levels[i - 1] - levels[i]) / (along[i] - along[i - 1]) > maxSlope;
            for (int k = 1; k < levels.Length; k++)
            {
                if (!Steep(k)) continue;
                int a = k - 1, b = k;
                for (int j = k + 1; j < levels.Length && along[j] - along[b] <= 3f; j++)
                    if (Steep(j)) b = j;
                int m = a + 1;
                float best = -1f;
                for (int i = a + 1; i <= b; i++)
                {
                    float slope = (levels[i - 1] - levels[i]) / Math.Max(1e-3f, along[i] - along[i - 1]);
                    if (slope > best) { best = slope; m = i; }
                }
                float high = levels[a], low = levels[b];
                k = b;
                if (high - low < MinFall) continue; // a riffle, not a fall: it keeps its slope
                for (int i = a + 1; i <= b; i++) levels[i] = i < m ? high : low;
                steps.Add(along[m]);
            }
            return steps;
        }

        /// <summary>A swap's <see cref="GroundSwap.Otherwise"/> meaning the ground round each tile.</summary>
        public const string Surround = "surround";

        private static string Swap(Grid g, GroundSwap w)
        {
            var from = new HashSet<string>(w.From);
            int n = 0;
            var refill = new HashSet<(int, int)>();
            HashSet<(int, int)>? small = w.Smaller > 0 ? SmallPatches(g, from, w.Smaller) : null;
            foreach ((int x, int y) in g.AllTiles().ToList())
            {
                if (small != null && !small.Contains((x, y))) continue;
                if (w.Rect != null && (x < w.Rect[0] || y < w.Rect[1] || x >= w.Rect[2] || y >= w.Rect[3])) continue;
                if (!(g.GroundAt(x, y) is string id) || !from.Contains(id)) continue;
                if (w.NearWater > 0 && !g.WaterWithin(x, y, w.NearWater)) continue;
                if (w.AwayFromWater > 0 && g.WaterWithin(x, y, w.AwayFromWater)) continue;
                if (w.Steep > 0f && !g.SteepWithin(x, y, w.SteepReach, w.Steep)) continue;
                string? to = w.To.FirstOrDefault(t => g.Has(t.Ground) && Patch(w.Seed + w.To.IndexOf(t), x, y, t))?.Ground;
                if (to == null && w.Otherwise == Surround) { refill.Add((x, y)); continue; }
                if (g.Set(x, y, to ?? w.Otherwise)) n++;
            }
            g.Fill(refill, from);
            n += refill.Count;
            return $"swap {string.Join("/", w.From)}: {n} tiles changed.";
        }

        /// <summary>The tiles of every patch of these grounds (joined edge to edge) smaller than <paramref name="under"/> tiles.</summary>
        private static HashSet<(int, int)> SmallPatches(Grid g, HashSet<string> grounds, int under)
        {
            var small = new HashSet<(int, int)>();
            var seen = new HashSet<(int, int)>();
            bool In((int x, int y) t) => g.GroundAt(t.x, t.y) is string id && grounds.Contains(id);
            foreach ((int x, int y) start in g.AllTiles())
            {
                if (seen.Contains(start) || !In(start)) continue;
                var patch = new List<(int, int)> { start };
                seen.Add(start);
                for (int k = 0; k < patch.Count; k++)
                {
                    (int x, int y) t = patch[k];
                    foreach (var n in new[] { (t.x + 1, t.y), (t.x - 1, t.y), (t.x, t.y + 1), (t.x, t.y - 1) })
                        if (!seen.Contains(n) && In(n)) { seen.Add(n); patch.Add(n); }
                }
                if (patch.Count < under) small.UnionWith(patch);
            }
            return small;
        }

        /// <summary>Whether a tile lies in one of a scatter's patches: smooth blobs with ragged edges.</summary>
        private static bool Patch(int seed, int x, int y, Scatter s) =>
            (LandNoise.Fbm(seed, x / Math.Max(1f, s.Scale), y / Math.Max(1f, s.Scale)) + 1f) * 0.5f + (LandNoise.Hash01(seed + 9, x, y) - 0.5f) * 0.2f < s.Share;

        // ------------------------------------------------------------------ the regions' tiles and heights

        private sealed class Grid
        {
            private readonly List<(RegionData region, Piece piece)> _parts = new List<(RegionData, Piece)>();
            private readonly Dictionary<string, GroundEdit.Brush> _brushes;
            private readonly Dictionary<(RegionData, char), string?> _ids = new Dictionary<(RegionData, char), string?>();
            private readonly Dictionary<RegionData, Heightfield> _heights = new Dictionary<RegionData, Heightfield>();
            private readonly HashSet<RegionData> _carved = new HashSet<RegionData>();

            public Grid(IReadOnlyList<RegionData> regions, ContinentData data)
            {
                _brushes = GroundEdit.Brushes(data).ToDictionary(b => b.Id);
                foreach (RegionData r in regions)
                    if (GroundEdit.Tiles(r) is Piece p && p.Rect != null) _parts.Add((r, p));
            }

            public bool Has(string id) => _brushes.ContainsKey(id);
            public bool IsWater(string? id) => id != null && _brushes.TryGetValue(id, out var b) && b.Kind.Water == true;
            /// <summary>Natural ground (not a made surface, not blocked): what banks and fringes may cover.</summary>
            public bool IsNatural(string id) => _brushes.TryGetValue(id, out var b) && !b.Kind.IsOverlay && !b.Kind.IsSoft && b.Kind.Blocks != true && b.Kind.Water != true;

            private (RegionData region, Piece piece)? PartAt(int x, int y)
            {
                foreach (var p in _parts)
                    if (x >= p.piece.Rect![0] && y >= p.piece.Rect[1] && x < p.piece.Rect[2] && y < p.piece.Rect[3]) return p;
                return null;
            }

            public IEnumerable<(int x, int y)> AllTiles()
            {
                foreach (var p in _parts)
                    for (int y = p.piece.Rect![1]; y < p.piece.Rect[3]; y++)
                        for (int x = p.piece.Rect[0]; x < p.piece.Rect[2]; x++) yield return (x, y);
            }

            public IEnumerable<(int x, int y)> TilesNear(List<float[]> pts, float reach)
            {
                int x0 = (int)Math.Floor(pts.Min(p => p[0]) - reach), x1 = (int)Math.Ceiling(pts.Max(p => p[0]) + reach);
                int y0 = (int)Math.Floor(pts.Min(p => p[1]) - reach), y1 = (int)Math.Ceiling(pts.Max(p => p[1]) + reach);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        if (PartAt(x, y) != null) yield return (x, y);
            }

            /// <summary>The ground id at a tile (null outside the regions or for a kind the layout doesn't have).</summary>
            public string? GroundAt(int x, int y)
            {
                if (!(PartAt(x, y) is var (region, piece)) || !(GroundEdit.At(piece, x, y) is char c)) return null;
                if (_ids.TryGetValue((region, c), out string? id)) return id;
                id = null;
                if (piece.Legend!.TryGetValue(c.ToString(), out TileKind kind))
                {
                    string colour = region.Materials.TryGetValue(kind.Material, out string m) ? m : "";
                    // Prefer the exact kind; a kind written before a ground changed its look counts by material and water.
                    id = _brushes.Values.FirstOrDefault(b => GroundEdit.Same(b.Kind, kind) && b.Colour == colour)?.Id
                         ?? _brushes.Values.FirstOrDefault(b => b.Kind.Material == kind.Material && (b.Kind.Water == true) == (kind.Water == true))?.Id;
                }
                return _ids[(region, c)] = id;
            }

            public bool Set(int x, int y, string id)
            {
                if (!(PartAt(x, y) is var (region, piece)) || !_brushes.TryGetValue(id, out var b)) return false;
                char c = GroundEdit.CharFor(region, piece, b.Kind, b.Materials, b.Letter);
                _ids[(region, c)] = id;
                return GroundEdit.Set(piece, x, y, c);
            }

            /// <summary>The commonest water ground beside a tile, or null.</summary>
            public string? WaterNear(int x, int y)
            {
                var votes = new Dictionary<string, int>();
                for (int j = -1; j <= 1; j++)
                    for (int i = -1; i <= 1; i++)
                        if ((i != 0 || j != 0) && GroundAt(x + i, y + j) is string id && IsWater(id))
                            votes[id] = votes.TryGetValue(id, out int v) ? v + 1 : 1;
                return votes.Count == 0 ? null : votes.OrderByDescending(v => v.Value).ThenBy(v => v.Key, StringComparer.Ordinal).First().Key;
            }

            /// <summary>How far a tile rises across its corners (0 without heights).</summary>
            public float RiseOf(int x, int y)
            {
                float lo = float.MaxValue, hi = float.MinValue;
                foreach ((int cx, int cy) in new[] { (x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1) })
                    if (Corner(cx, cy) is float h) { lo = Math.Min(lo, h); hi = Math.Max(hi, h); }
                return hi > lo ? hi - lo : 0f;
            }

            public bool SteepWithin(int x, int y, int r, float steep)
            {
                for (int j = y - r; j <= y + r; j++)
                    for (int i = x - r; i <= x + r; i++)
                        if (GroundAt(i, j) != null && RiseOf(i, j) > steep) return true;
                return false;
            }

            public bool WaterWithin(int x, int y, int r)
            {
                for (int j = y - r; j <= y + r; j++)
                    for (int i = x - r; i <= x + r; i++)
                        if (IsWater(GroundAt(i, j))) return true;
                return false;
            }

            /// <summary>Cleared tiles take the commonest ground round them that isn't being cleared, from the outside in.</summary>
            public void Fill(HashSet<(int, int)> cleared, HashSet<string> erase)
            {
                var left = new HashSet<(int, int)>(cleared);
                for (int pass = 0; pass < 40 && left.Count > 0; pass++)
                {
                    var done = new List<((int, int) t, string id)>();
                    foreach ((int x, int y) t in left)
                    {
                        var votes = new Dictionary<string, int>();
                        for (int j = -1; j <= 1; j++)
                            for (int i = -1; i <= 1; i++)
                            {
                                if (i == 0 && j == 0 || left.Contains((t.x + i, t.y + j))) continue;
                                string? id = GroundAt(t.x + i, t.y + j);
                                if (id == null || erase.Contains(id) || IsWater(id) || !IsNatural(id)) continue;
                                votes[id] = votes.TryGetValue(id, out int v) ? v + 1 : 1;
                            }
                        if (votes.Count > 0) done.Add((t, votes.OrderByDescending(v => v.Value).ThenBy(v => v.Key, StringComparer.Ordinal).First().Key));
                    }
                    if (done.Count == 0) break;
                    foreach (var d in done) { Set(d.t.Item1, d.t.Item2, d.id); left.Remove(d.t); }
                }
                foreach ((int x, int y) t in left) Set(t.x, t.y, "grass");
            }

            // Heights: one field per region; a corner on a border is written to every region that has it.

            private Heightfield? FieldOf(RegionData r)
            {
                if (_heights.TryGetValue(r, out Heightfield f)) return f;
                Heightfield? read = TerrainText.Read(r);
                if (read != null) _heights[r] = read;
                return read;
            }

            public float? Corner(int cx, int cy)
            {
                foreach (var p in _parts)
                    if (FieldOf(p.region) is Heightfield f && cx >= f.OriginX && cy >= f.OriginY && cx <= f.OriginX + f.Width && cy <= f.OriginY + f.Depth)
                        return f.Corner(cx, cy);
                return null;
            }

            /// <summary>Only corners every tile round which is in these regions change, so borders with others stay matched.</summary>
            public void SetCorner(int cx, int cy, float h)
            {
                if (PartAt(cx - 1, cy - 1) == null || PartAt(cx, cy - 1) == null || PartAt(cx - 1, cy) == null || PartAt(cx, cy) == null) return;
                foreach (var p in _parts)
                    if (FieldOf(p.region) is Heightfield f && cx >= f.OriginX && cy >= f.OriginY && cx <= f.OriginX + f.Width && cy <= f.OriginY + f.Depth)
                    {
                        f.SetCorner(cx, cy, h);
                        _carved.Add(p.region);
                    }
            }

            /// <summary>The water's level at each point of the line: the lowest old water near it, never rising downstream.</summary>
            public float[] Levels(List<float[]> line, List<(int x, int y)> water)
            {
                var levels = new float[line.Count];
                for (int k = 0; k < line.Count; k++)
                {
                    float best = float.NaN;
                    foreach ((int x, int y) in water)
                    {
                        if (Math.Abs(x + 0.5f - line[k][0]) > 5f || Math.Abs(y + 0.5f - line[k][1]) > 5f) continue;
                        foreach ((int cx, int cy) in new[] { (x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1) })
                            if (Corner(cx, cy) is float h && (float.IsNaN(best) || h < best)) best = h;
                    }
                    levels[k] = best;
                }
                // Gaps take the nearest known level; then the level only falls downstream.
                for (int k = 0; k < levels.Length; k++)
                    if (float.IsNaN(levels[k]))
                    {
                        int a = k - 1, b = k + 1;
                        while (a >= 0 && float.IsNaN(levels[a])) a--;
                        while (b < levels.Length && float.IsNaN(levels[b])) b++;
                        levels[k] = a >= 0 ? levels[a] : b < levels.Length ? levels[b] : (Corner((int)line[k][0], (int)line[k][1]) ?? 0f);
                    }
                for (int k = 1; k < levels.Length; k++) levels[k] = Math.Min(levels[k], levels[k - 1]);
                return levels;
            }

            /// <summary>The new bed down to its level, banks easing down to it, and the old bed filled to the land round it.</summary>
            public void Carve(List<(int x, int y)> bed, List<(int x, int y)> oldBed, List<float[]> line, float[] levels)
            {
                float LevelAt(float x, float y)
                {
                    int best = 0;
                    float bd = float.MaxValue;
                    for (int k = 0; k < line.Count; k++)
                    {
                        float d = Sq(line[k][0] - x) + Sq(line[k][1] - y);
                        if (d < bd) { bd = d; best = k; }
                    }
                    return levels[best];
                }
                var bedCorners = new HashSet<(int, int)>();
                foreach ((int x, int y) in bed)
                    foreach (var c in new[] { (x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1) }) bedCorners.Add(c);
                var oldCorners = new HashSet<(int, int)>();
                foreach ((int x, int y) in oldBed)
                    foreach (var c in new[] { (x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1) }) if (!bedCorners.Contains(c)) oldCorners.Add(c);
                // The old bed: the mean of the land round it that was never water.
                var fills = new List<((int, int) c, float h)>();
                foreach ((int cx, int cy) in oldCorners)
                {
                    float sum = 0f;
                    int n = 0;
                    for (int j = -4; j <= 4; j++)
                        for (int i = -4; i <= 4; i++)
                        {
                            var o = (cx + i, cy + j);
                            if (oldCorners.Contains(o) || bedCorners.Contains(o) || !(Corner(o.Item1, o.Item2) is float h)) continue;
                            sum += h; n++;
                        }
                    if (n > 0) fills.Add(((cx, cy), sum / n));
                }
                foreach (var f in fills) SetCorner(f.c.Item1, f.c.Item2, f.h);
                foreach ((int cx, int cy) in bedCorners)
                    if (Corner(cx, cy) is float h) SetCorner(cx, cy, Math.Min(h, LevelAt(cx, cy)));
                // Banks: within three tiles the land eases down towards the water, never below it.
                const float reach = 3f, bank = 0.08f;
                int minX = bedCorners.Min(c => c.Item1) - 3, maxX = bedCorners.Max(c => c.Item1) + 3;
                int minY = bedCorners.Min(c => c.Item2) - 3, maxY = bedCorners.Max(c => c.Item2) + 3;
                for (int cy = minY; cy <= maxY; cy++)
                    for (int cx = minX; cx <= maxX; cx++)
                    {
                        if (bedCorners.Contains((cx, cy)) || !(Corner(cx, cy) is float h)) continue;
                        float d = float.MaxValue;
                        for (int j = -3; j <= 3; j++)
                            for (int i = -3; i <= 3; i++)
                                if (bedCorners.Contains((cx + i, cy + j))) d = Math.Min(d, (float)Math.Sqrt(i * i + j * j));
                        if (d > reach) continue;
                        float level = LevelAt(cx, cy) + bank;
                        float eased = level + (h - level) * LandNoise.Smooth(d / reach);
                        if (eased < h) SetCorner(cx, cy, Math.Max(level, eased));
                    }
            }

            public void WriteTerrain()
            {
                foreach (RegionData r in _carved)
                    r.Terrain = TerrainText.Write(_heights[r], r.Terrain?.CliffStep ?? 1f);
            }
        }

        private static float DistToPolyline(List<float[]> pts, float px, float py)
        {
            float best = float.MaxValue;
            for (int k = 0; k + 1 < pts.Count; k++)
            {
                float[] a = pts[k], b = pts[k + 1];
                float dx = b[0] - a[0], dy = b[1] - a[1], len2 = dx * dx + dy * dy;
                float t = len2 > 0 ? Math.Max(0f, Math.Min(1f, ((px - a[0]) * dx + (py - a[1]) * dy) / len2)) : 0f;
                best = Math.Min(best, (float)Math.Sqrt(Sq(a[0] + t * dx - px) + Sq(a[1] + t * dy - py)));
            }
            return best;
        }

        private static float Dist(float[] a, float[] b) => (float)Math.Sqrt(Sq(a[0] - b[0]) + Sq(a[1] - b[1]));
        private static float Sq(float v) => v * v;
    }
}
