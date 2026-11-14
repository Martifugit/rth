using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Shared.World.Ground;

namespace Rth.Shared.World.Data
{
    /// <summary>
    /// Re-cuts the open land's regions (land_*) onto a grid of <see cref="Size"/> tiles, so each
    /// region's Unity terrain has a height sample on every tile corner (129 a side: a terrain's
    /// samples must number a power of two plus one). Every tile's ground, every corner's height,
    /// every piece and every stone comes across exactly; a new region takes its title, settings and
    /// area from the old region that covered most of it.
    /// </summary>
    public static class Regrid
    {
        public const int Size = 128;

        public sealed class Result
        {
            public List<RegionData> Regions = new List<RegionData>();
            /// <summary>New region id to the old region that covered most of it.</summary>
            public Dictionary<string, string> MostlyFrom = new Dictionary<string, string>(StringComparer.Ordinal);
            public List<string> Messages = new List<string>();
        }

        public static string Part(int n) => n < 0 ? "m" + (-n) : n.ToString();
        private static int Cell(int tile) => (int)Math.Floor(tile / (double)Size);

        public static Result Run(IReadOnlyList<RegionData> land, GroundTable table)
        {
            var result = new Result();
            // Which old region holds each tile's ground, and each corner's height.
            var owner = new Dictionary<(int, int), (RegionData region, Piece piece)>();
            var heights = new Dictionary<(int, int), float>();
            var overlap = new Dictionary<(int i, int j), Dictionary<string, int>>();
            var extent = new Dictionary<(int i, int j), int[]>(); // the old land inside each cell: x0, y0, x1, y1
            foreach (RegionData r in land)
            {
                Piece? piece = GroundEdit.Tiles(r);
                for (int y = r.OriginY; y < r.OriginY + r.Height; y++)
                    for (int x = r.OriginX; x < r.OriginX + r.Width; x++)
                    {
                        var key = (Cell(x), Cell(y));
                        if (!overlap.TryGetValue(key, out var counts)) overlap[key] = counts = new Dictionary<string, int>(StringComparer.Ordinal);
                        counts[r.Id] = counts.TryGetValue(r.Id, out int n) ? n + 1 : 1;
                        if (!extent.TryGetValue(key, out int[] e)) extent[key] = e = new[] { x, y, x + 1, y + 1 };
                        e[0] = Math.Min(e[0], x); e[1] = Math.Min(e[1], y); e[2] = Math.Max(e[2], x + 1); e[3] = Math.Max(e[3], y + 1);
                        if (piece != null) owner[(x, y)] = (r, piece);
                    }
                Heightfield field = TerrainText.ReadOrFlat(r);
                for (int y = r.OriginY; y <= r.OriginY + r.Height; y++)
                    for (int x = r.OriginX; x <= r.OriginX + r.Width; x++)
                        if (!heights.ContainsKey((x, y))) heights[(x, y)] = field.Corner(x, y);
            }
            var byId = land.ToDictionary(r => r.Id, StringComparer.Ordinal);

            foreach (KeyValuePair<(int i, int j), Dictionary<string, int>> cell in overlap.OrderBy(c => c.Key.j).ThenBy(c => c.Key.i))
            {
                (int i, int j) = cell.Key;
                RegionData most = byId[cell.Value.OrderByDescending(e => e.Value).ThenBy(e => e.Key, StringComparer.Ordinal).First().Key];
                string id = $"land_{Part(i)}_{Part(j)}";
                var region = new RegionData
                {
                    Id = id, Title = most.Title, Scene = $"Assets/Scenes/Land/Land_{Part(i)}_{Part(j)}.unity",
                    // Only the land that was there: a cell at the world's edge may be smaller (its terrain still spans the cell).
                    Origin = new[] { extent[cell.Key][0], extent[cell.Key][1] },
                    Size = new[] { extent[cell.Key][2] - extent[cell.Key][0], extent[cell.Key][3] - extent[cell.Key][1] }, Plane = most.Plane, Instanced = most.Instanced,
                    Music = most.Music, Atmosphere = most.Atmosphere, PostFx = most.PostFx, Surround = most.Surround, Ground = most.Ground, Seed = most.Seed,
                };
                result.MostlyFrom[id] = most.Id;

                // Pieces, by the old region they came from (each lies inside one new cell).
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (string from in cell.Value.Keys.OrderBy(k => k, StringComparer.Ordinal))
                {
                    RegionData old = byId[from];
                    foreach (Piece p in old.Pieces)
                    {
                        if (p.Kind == PieceKind.Tiles || !(Anchor(p) is (int x, int y)) || Cell(x) != i || Cell(y) != j) continue;
                        foreach (string? m in new[] { p.Material, p.Walls, p.Floor, p.Roof })
                            if (m != null && old.Materials.TryGetValue(m, out string colour)) AddMaterial(region, m, colour, result, id);
                        foreach (string m in old.Materials.Keys.Where(k => !k.StartsWith("Land", StringComparison.Ordinal)))
                            AddMaterial(region, m, old.Materials[m], result, id);
                        string pid = ids.Add(p.Id) ? p.Id : $"{from}-{p.Id}";
                        ids.Add(pid);
                        Piece copy = RegionFiles.ParseRegion(RegionFiles.Format(new RegionData { Id = "x", Pieces = { p } })).Pieces[0];
                        copy.Id = pid;
                        region.Pieces.Add(copy);
                    }
                }

                // The ground: every tile's from the old region that held it, the heights on every corner.
                int x0 = region.OriginX, y0 = region.OriginY, w = region.Width, h = region.Height;
                var ground = new Piece
                {
                    Id = "ground", Kind = PieceKind.Tiles, Rect = new[] { x0, y0, x0 + w, y0 + h },
                    Legend = new SortedDictionary<string, TileKind>(StringComparer.Ordinal),
                    Rows = Enumerable.Repeat(new string(' ', w), h).ToList(),
                };
                region.Pieces.Insert(0, ground);
                WorldGround.Apply(region, table,
                    (x, y) => owner.TryGetValue((x, y), out var o) ? WorldGround.GroundAt(o.region, o.piece, table, x, y) : null,
                    (x, y) => heights.TryGetValue((x, y), out float h) ? h : (float?)null);
                if (ground.Rows!.All(r => r.Trim().Length == 0) && region.Pieces.Count == 1) continue;

                // Stones, as they lay.
                for (int y = y0; y < y0 + h; y++)
                    for (int x = x0; x < x0 + w; x++)
                    {
                        if (!owner.TryGetValue((x, y), out var o) || !(GroundEdit.StoneAt(o.piece, x, y) is StoneKind s)) continue;
                        string name = AddMaterial(region, s.Material, o.region.Materials.TryGetValue(s.Material, out string c) ? c : "", result, id);
                        GroundEdit.SetStone(ground, x, y, GroundEdit.StoneCharFor(ground, name, s.Share));
                    }
                GroundEdit.PruneStones(ground);
                // Painted, with the ring of the neighbours' tiles, as the ground was drawn (the terrains are made from it).
                ground.Paint = true;
                GroundEdit.EnsureRing(ground, 4);
                result.Regions.Add(region);
            }
            GroundEdit.RefreshSurrounds(result.Regions);
            return result;
        }

        /// <summary>The tile a piece stands on or starts at.</summary>
        private static (int x, int y)? Anchor(Piece p)
        {
            if (p.At != null) return (p.At[0], p.At[1]);
            if (p.Rect != null) return (p.Rect[0], p.Rect[1]);
            if (p.From != null) return (p.From[0], p.From[1]);
            if (p.Line != null && p.Line.Count > 0) return ((int)Math.Floor(p.Line[0][0]), (int)Math.Floor(p.Line[0][1]));
            return null;
        }

        /// <summary>Adds a material to the region; one whose name it already colours otherwise is a problem worth reporting.</summary>
        private static string AddMaterial(RegionData region, string name, string colour, Result result, string id)
        {
            if (region.Materials.TryGetValue(name, out string have) && have != colour)
            {
                result.Messages.Add($"{id}: material '{name}' is {have} in one old region and {colour} in another; kept {have}.");
                return name;
            }
            region.Materials[name] = colour;
            return name;
        }
    }
}
