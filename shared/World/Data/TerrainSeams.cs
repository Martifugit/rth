using System;
using System.Collections.Generic;
using System.Linq;

namespace Rth.Shared.World.Data
{
    /// <summary>
    /// Keeps the ground whole across region borders. Each region stores the heights of its own edge
    /// corners, so two regions side by side hold two copies of the corners they share; whenever a
    /// region's terrain is written (sculpted in the Editor with its neighbour closed, a stroke plan,
    /// `tools/world terrain`), its neighbours' copies are brought into line here, as the rings of
    /// tiles follow painted ground. Generated regions are never written: the blockout pins them to
    /// the built regions' edges, so those are reported for a `blockout` instead.
    /// </summary>
    public static class TerrainSeams
    {
        public sealed class Result
        {
            /// <summary>Regions whose terrain this changed (to be saved).</summary>
            public List<string> Changed = new List<string>();
            /// <summary>Generated regions whose edge no longer meets a built neighbour's: run the blockout.</summary>
            public List<string> GeneratedNeighbours = new List<string>();
            /// <summary>Notes worth showing: an edge moved so far that the tiles beside it may have turned to cliff.</summary>
            public List<string> Messages = new List<string>();
        }

        /// <summary>How far an edge may move before it is worth a note (in tiles).</summary>
        public const float NotableMove = 0.5f;

        /// <summary>
        /// Brings the corners along every border into agreement. Where one region of a pair is in
        /// <paramref name="changed"/> (what was just written), its edge wins; where both are, or
        /// <paramref name="changed"/> is null (mending the whole world), they meet halfway. A built
        /// region always wins over a generated one, which is left for the blockout. The regions in
        /// <see cref="Result.Changed"/> have their <see cref="RegionData.Terrain"/> rewritten.
        /// </summary>
        public static Result Follow(IReadOnlyList<RegionData> regions, ICollection<string>? changed)
        {
            var result = new Result();
            var fields = new Dictionary<RegionData, Heightfield>();
            var touched = new HashSet<RegionData>();
            Heightfield Field(RegionData r) => fields.TryGetValue(r, out Heightfield f) ? f : fields[r] = TerrainText.ReadOrFlat(r);
            bool Wrote(RegionData r) => changed == null || changed.Contains(r.Id);

            var land = regions.Where(r => !r.Instanced && r.Width > 0 && r.Height > 0).ToList();
            for (int i = 0; i < land.Count; i++)
                for (int j = i + 1; j < land.Count; j++)
                {
                    RegionData a = land[i], b = land[j];
                    if (a.Terrain == null && b.Terrain == null) continue;
                    List<(int x, int y)> corners = SharedCorners(a, b);
                    if (corners.Count == 0) continue;
                    bool aWins = Wrote(a), bWins = Wrote(b);
                    if (!aWins && !bWins) continue;
                    // Built ground wins over generated ground, which the blockout pins to it.
                    if (a.Generated != null && b.Generated == null) { aWins = false; bWins = true; }
                    else if (b.Generated != null && a.Generated == null) { aWins = true; bWins = false; }
                    float moved = 0f;
                    bool differ = false;
                    foreach ((int x, int y) in corners)
                    {
                        int ha = TerrainText.Hundredths(Field(a).Corner(x, y)), hb = TerrainText.Hundredths(Field(b).Corner(x, y));
                        if (ha == hb) continue;
                        differ = true;
                        int to = aWins && bWins ? (int)Math.Round((ha + hb) / 2.0, MidpointRounding.AwayFromZero) : aWins ? ha : hb;
                        moved = Math.Max(moved, Math.Max(Math.Abs(to - ha), Math.Abs(to - hb)) * TerrainText.Step);
                        if (to != ha && a.Generated == null) { Field(a).SetCorner(x, y, to * TerrainText.Step); touched.Add(a); }
                        if (to != hb && b.Generated == null) { Field(b).SetCorner(x, y, to * TerrainText.Step); touched.Add(b); }
                    }
                    if (!differ) continue;
                    foreach (RegionData g in new[] { a, b })
                        if (g.Generated != null && !result.GeneratedNeighbours.Contains(g.Id)) result.GeneratedNeighbours.Add(g.Id);
                    if (moved > NotableMove)
                        result.Messages.Add(FormattableString.Invariant($"The border of '{a.Id}' and '{b.Id}' moved by up to {moved:0.##} tiles to meet; look at the tiles beside it."));
                }
            foreach (RegionData r in touched)
            {
                r.Terrain = TerrainText.Write(fields[r], r.Terrain?.CliffStep ?? 1f);
                result.Changed.Add(r.Id);
            }
            result.Changed.Sort(StringComparer.Ordinal);
            return result;
        }

        /// <summary>The corners two regions side by side on one plane share along their border (none if they don't touch along an edge).</summary>
        public static List<(int x, int y)> SharedCorners(RegionData a, RegionData b)
        {
            var corners = new List<(int x, int y)>();
            if (a.Instanced || b.Instanced || a.Plane != b.Plane) return corners;
            int ax1 = a.OriginX + a.Width, ay1 = a.OriginY + a.Height, bx1 = b.OriginX + b.Width, by1 = b.OriginY + b.Height;
            int oy0 = Math.Max(a.OriginY, b.OriginY), oy1 = Math.Min(ay1, by1), ox0 = Math.Max(a.OriginX, b.OriginX), ox1 = Math.Min(ax1, bx1);
            if ((ax1 == b.OriginX || bx1 == a.OriginX) && oy0 < oy1)
            {
                int x = ax1 == b.OriginX ? ax1 : a.OriginX;
                for (int y = oy0; y <= oy1; y++) corners.Add((x, y));
            }
            else if ((ay1 == b.OriginY || by1 == a.OriginY) && ox0 < ox1)
            {
                int y = ay1 == b.OriginY ? ay1 : a.OriginY;
                for (int x = ox0; x <= ox1; x++) corners.Add((x, y));
            }
            return corners;
        }
    }
}
