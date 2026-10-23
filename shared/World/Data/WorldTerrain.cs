using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rth.Shared.World.Layout;

namespace Rth.Shared.World.Data
{
    /// <summary>
    /// The whole surface world's ground heights as one field, for editing them in one go (the
    /// Editor's terrain editor, docs/workflow.md), and the way edited heights get back into the data.
    /// Ground drawn by a <see cref="PieceKind.Tiles"/> piece can be sculpted; regions drawn another
    /// way (Tolling, Palmerway: hand-made flat ground) are pinned, and so are the corners they share.
    /// <see cref="Save"/> writes a built region's heights into its <c>terrain</c>, and a generated
    /// region's as a raise over the layout's heights (<see cref="HeightEdits"/>), so the blockout keeps
    /// it; then it regenerates the blockout and exports the maps, as <c>tools/world</c> would.
    /// </summary>
    public sealed class WorldTerrain
    {
        public sealed class Area
        {
            public RegionData Region = new RegionData();
            public string Path = "";
            /// <summary>It has tile ground to sculpt.</summary>
            public bool Editable;
            public bool Contains(int x, int y) => x >= Region.OriginX && y >= Region.OriginY && x <= Region.OriginX + Region.Width && y <= Region.OriginY + Region.Height;
        }

        public sealed class Result
        {
            /// <summary>Regions whose heights changed.</summary>
            public readonly List<string> Regions = new List<string>();
            public readonly List<string> Messages = new List<string>();
            public readonly List<string> Errors = new List<string>();
        }

        public string Root = "";
        /// <summary>Every surface region's corners, over the rectangle that holds them all; 0 between regions.</summary>
        public Heightfield Field = null!;
        public readonly List<Area> Areas = new List<Area>();
        private TileKind?[] _kinds = Array.Empty<TileKind?>();
        private Area?[] _owners = Array.Empty<Area?>();
        private bool[] _pinned = Array.Empty<bool>();

        /// <summary>The tile's kind of ground and the region it is in, or null where there is no tile ground.</summary>
        public (TileKind kind, Area area)? Tile(int x, int y)
        {
            if (!Field.ContainsTile(x, y)) return null;
            int i = (y - Field.OriginY) * Field.Width + (x - Field.OriginX);
            TileKind? kind = _kinds[i];
            Area? owner = _owners[i];
            return kind == null || owner == null ? ((TileKind, Area)?)null : (kind, owner);
        }

        /// <summary>A corner that saving leaves as it is: on or in a region without tile ground, or in no region.</summary>
        public bool Pinned(int x, int y) => !Field.Contains(x, y) || _pinned[(y - Field.OriginY) * (Field.Width + 1) + (x - Field.OriginX)];

        public static WorldTerrain Load(string root)
        {
            var world = new WorldTerrain { Root = root };
            string dir = System.IO.Path.Combine(root, "world", "regions");
            foreach (string path in RegionFiles.RegionPaths(dir))
            {
                RegionData region = RegionFiles.LoadRegion(path);
                if (region.Instanced || region.Plane != 0 || region.Width <= 0 || region.Height <= 0) continue;
                world.Areas.Add(new Area { Region = region, Path = path, Editable = region.Pieces.Any(p => p.Kind == PieceKind.Tiles) });
            }
            if (world.Areas.Count == 0) throw new InvalidOperationException("No surface regions in world/regions.");
            int x0 = world.Areas.Min(a => a.Region.OriginX), y0 = world.Areas.Min(a => a.Region.OriginY);
            int x1 = world.Areas.Max(a => a.Region.OriginX + a.Region.Width), y1 = world.Areas.Max(a => a.Region.OriginY + a.Region.Height);
            world.Field = new Heightfield(x0, y0, x1 - x0, y1 - y0);
            int w = x1 - x0, h = y1 - y0;
            world._kinds = new TileKind?[w * h];
            world._owners = new Area?[w * h];
            world._pinned = Enumerable.Repeat(true, (w + 1) * (h + 1)).ToArray();

            foreach (Area a in world.Areas.Where(a => a.Editable))
            {
                RegionData r = a.Region;
                Heightfield own = TerrainText.ReadOrFlat(r);
                for (int y = r.OriginY; y <= r.OriginY + r.Height; y++)
                    for (int x = r.OriginX; x <= r.OriginX + r.Width; x++)
                    {
                        world.Field.SetCorner(x, y, own.Corner(x, y));
                        world._pinned[(y - y0) * (w + 1) + (x - x0)] = false;
                    }
                foreach (Piece p in r.Pieces.Where(p => p.Kind == PieceKind.Tiles && p.Rect != null && p.Rows != null && p.Legend != null))
                    for (int row = 0; row < p.Rows!.Count; row++)
                    {
                        int y = p.Rect![3] - 1 - row;
                        string line = p.Rows[row];
                        for (int col = 0; col < line.Length; col++)
                        {
                            int x = p.Rect[0] + col;
                            if (line[col] == ' ' || x < x0 || y < y0 || x >= x1 || y >= y1) continue;
                            if (!p.Legend!.TryGetValue(line[col].ToString(), out TileKind kind)) continue;
                            world._kinds[(y - y0) * w + (x - x0)] = kind;
                            world._owners[(y - y0) * w + (x - x0)] = a;
                        }
                    }
            }
            // Regions without tile ground keep their heights, and so do the corners they share.
            foreach (Area a in world.Areas.Where(a => !a.Editable))
            {
                RegionData r = a.Region;
                Heightfield own = TerrainText.ReadOrFlat(r);
                for (int y = r.OriginY; y <= r.OriginY + r.Height; y++)
                    for (int x = r.OriginX; x <= r.OriginX + r.Width; x++)
                    {
                        world.Field.SetCorner(x, y, own.Corner(x, y));
                        world._pinned[(y - y0) * (w + 1) + (x - x0)] = true;
                    }
            }
            return world;
        }

        /// <summary>
        /// Writes edited heights (absolute corners, in tiles; any rectangle) back into the data: built
        /// regions' <c>terrain</c>, generated regions' raise in world/layout/sculpt, then the blockout
        /// and the server's maps. Pinned corners and heights the same to the hundredth are left alone.
        /// Run it on .NET (<c>tools/world terrain-save</c>), not in Unity: Unity's Mono rounds floats
        /// differently, and the blockout it would draw differs from <c>tools/world</c>'s by a tile here and there.
        /// </summary>
        /// <summary>
        /// Edited heights as a file, for handing from the Editor to <c>tools/world terrain-save</c>:
        /// "RTHH", the origin and size as four ints, then each corner in hundredths of a tile as a
        /// short, row by row from the south (little-endian).
        /// </summary>
        public static void WriteHeights(string path, Heightfield field)
        {
            using var w = new BinaryWriter(File.Create(path));
            w.Write(new[] { (byte)'R', (byte)'T', (byte)'H', (byte)'H' });
            w.Write(field.OriginX);
            w.Write(field.OriginY);
            w.Write(field.Width);
            w.Write(field.Depth);
            foreach (float h in field.Corners) w.Write((short)TerrainText.Hundredths(Math.Max(-4f, Math.Min(16f, h))));
        }

        public static Heightfield ReadHeights(string path)
        {
            using var r = new BinaryReader(File.OpenRead(path));
            if (new string(r.ReadChars(4)) != "RTHH") throw new FormatException(path + " is not a heights file.");
            int x0 = r.ReadInt32(), y0 = r.ReadInt32(), w = r.ReadInt32(), h = r.ReadInt32();
            var corners = new float[(w + 1) * (h + 1)];
            for (int i = 0; i < corners.Length; i++) corners[i] = r.ReadInt16() * TerrainText.Step;
            return new Heightfield(x0, y0, w, h, corners);
        }

        public static Result Save(string root, Heightfield edited)
        {
            var result = new Result();
            WorldTerrain world = Load(root);
            // What each corner becomes: the edit, to the hundredth, unless it is pinned.
            Heightfield now = world.Field;
            var final = new Heightfield(now.OriginX, now.OriginY, now.Width, now.Depth, now.Corners);
            int pinnedTouched = 0;
            for (int y = Math.Max(now.OriginY, edited.OriginY); y <= Math.Min(now.OriginY + now.Depth, edited.OriginY + edited.Depth); y++)
                for (int x = Math.Max(now.OriginX, edited.OriginX); x <= Math.Min(now.OriginX + now.Width, edited.OriginX + edited.Width); x++)
                {
                    float h = TerrainText.Hundredths(Math.Max(-4f, Math.Min(16f, edited.Corner(x, y)))) * TerrainText.Step;
                    if (TerrainText.Hundredths(h) == TerrainText.Hundredths(now.Corner(x, y))) continue;
                    if (world.Pinned(x, y)) pinnedTouched++;
                    else final.SetCorner(x, y, h);
                }
            float Final(int x, int y) => final.Corner(x, y);
            bool Changed(RegionData r)
            {
                for (int y = r.OriginY; y <= r.OriginY + r.Height; y++)
                    for (int x = r.OriginX; x <= r.OriginX + r.Width; x++)
                        if (TerrainText.Hundredths(Final(x, y)) != TerrainText.Hundredths(now.Corner(x, y))) return true;
                return false;
            }
            Heightfield FieldOf(RegionData r)
            {
                var f = new Heightfield(r.OriginX, r.OriginY, r.Width, r.Height);
                for (int y = r.OriginY; y <= r.OriginY + r.Height; y++)
                    for (int x = r.OriginX; x <= r.OriginX + r.Width; x++)
                        f.SetCorner(x, y, Final(x, y));
                return f;
            }

            // Built regions first: one that gains terrain changes how the generated land meets it.
            var generated = new List<Area>();
            foreach (Area a in world.Areas.Where(a => a.Editable && Changed(a.Region)))
            {
                if (a.Region.Generated != null)
                {
                    generated.Add(a);
                    continue;
                }
                a.Region.Terrain = TerrainText.Write(FieldOf(a.Region), a.Region.Terrain?.CliffStep ?? 1f);
                RegionFiles.SaveRegion(a.Region, a.Path);
                result.Regions.Add(a.Region.Id);
            }
            if (pinnedTouched > 0)
                result.Messages.Add($"{pinnedTouched} corners on regions without tile ground ({string.Join(", ", world.Areas.Where(a => !a.Editable).Select(a => a.Region.Id))}) were left as they are.");

            if (generated.Count > 0)
            {
                LayoutWorkspace layout = LayoutWorkspace.Load(root);
                if (layout.Map.Count == 0)
                {
                    result.Errors.AddRange(layout.Map.Errors.Select(e => "layout: " + e));
                    return result;
                }
                LandHeights land = Blockout.Heights(layout.Map, layout.Built);
                foreach (Area a in generated)
                {
                    RegionData r = a.Region;
                    var raise = new Heightfield(r.OriginX, r.OriginY, r.Width, r.Height);
                    for (int y = r.OriginY; y <= r.OriginY + r.Height; y++)
                        for (int x = r.OriginX; x <= r.OriginX + r.Width; x++)
                            raise.SetCorner(x, y, (TerrainText.Hundredths(Final(x, y)) - TerrainText.Hundredths(land.Corner(x, y))) * TerrainText.Step);
                    HeightEdits.Save(root, r.Id, raise);
                    result.Regions.Add(r.Id);
                }
                // The blockout draws them again with the raise on top.
                layout = LayoutWorkspace.Load(root);
                Blockout.Result blockout = Blockout.Generate(layout.Map, layout.Built, layout.Edits);
                (Dictionary<string, string> write, List<string> delete) = Blockout.Changes(System.IO.Path.Combine(root, "world", "regions"), blockout);
                foreach (KeyValuePair<string, string> f in write) File.WriteAllText(f.Key, f.Value);
                if (delete.Count > 0) result.Messages.Add($"{delete.Count} generated regions are no longer in the layout: run `tools/world blockout`.");
            }

            if (result.Regions.Count > 0)
            {
                WorldWorkspace ws = WorldWorkspace.Load(root);
                foreach (string e in ws.LoadErrors) result.Errors.Add(e);
                foreach (WorldWorkspace.Entry e in ws.Regions.Where(e => result.Regions.Contains(e.Region.Id)))
                    foreach (string error in e.Compiled.Errors) result.Errors.Add($"{e.Region.Id}: {error}");
                foreach (string path in ws.Export()) result.Messages.Add("wrote " + System.IO.Path.GetFileName(path));
            }
            return result;
        }
    }
}
