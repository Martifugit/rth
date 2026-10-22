using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Shared.Progress;
using Rth.Shared.World;

namespace Rth.Server.Content
{
    /// <summary>
    /// Works out the bestiary's guide from content and the map, so nothing about it is listed by
    /// hand:
    /// <list type="bullet">
    /// <item>A creature (an attackable NPC) lives in every region one of its spawns is in.</item>
    /// <item>Its carcass is an item it drops that a recipe takes as an input; that recipe's
    /// outputs and output table count as the creature's drops (the drop log credits them to it).</item>
    /// <item>A drop is <b>notable</b> (the bestiary hints at it) when it is rare, at most
    /// <see cref="WorldSettings.RareDropChance"/> per kill, or unique, given by no other creature
    /// or boss and not guaranteed. Guaranteed, common, shared drops are left to the drop log.</item>
    /// </list>
    /// Chances are per kill (per completion for a boss), counting every roll and nested table.
    /// </summary>
    public static class BestiaryGuideBuilder
    {
        private const int MaxDepth = 8;

        public static BestiaryGuide Build(GameContent content, IEnumerable<NpcSpawn> spawns, IEnumerable<WorldRegion> regions)
        {
            List<WorldRegion> regionList = regions.ToList();
            Dictionary<string, string> carcassRecipes = CarcassRecipes(content);
            var guide = new BestiaryGuide();

            // Every source's drops, with chances, so uniqueness can be judged across all of them.
            var creatureDrops = new Dictionary<string, Dictionary<string, double>>();
            var carcasses = new Dictionary<string, BestiaryCarcass>();
            List<NpcDef> creatures = content.Npcs.Values.Where(n => n.Attackable).OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (NpcDef npc in creatures)
            {
                Dictionary<string, double> drops = npc.DropTable != null ? TableChances(content, npc.DropTable, 0) : new Dictionary<string, double>();
                // The carcass: the first recipe (by id) that takes something it drops.
                foreach (KeyValuePair<string, string> pair in carcassRecipes.Where(p => p.Value == npc.Id).OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    RecipeDef recipe = content.Recipes[pair.Key];
                    ItemAmount? input = recipe.Inputs.FirstOrDefault(i => drops.ContainsKey(i.Item));
                    if (input == null) continue;
                    double carcassChance = drops[input.Item];
                    var yields = new Dictionary<string, double>();
                    foreach (ItemAmount output in recipe.Outputs) Merge(yields, output.Item, 1.0);
                    if (recipe.OutputTable != null)
                        foreach (KeyValuePair<string, double> y in TableChances(content, recipe.OutputTable, 0)) Merge(yields, y.Key, y.Value);
                    foreach (KeyValuePair<string, double> y in yields) Merge(drops, y.Key, y.Value * carcassChance);
                    carcasses[npc.Id] = new BestiaryCarcass { Item = input.Item, Recipe = recipe.Id, Skill = recipe.Skill, Level = recipe.Level, Station = recipe.Station };
                    break;
                }
                creatureDrops[npc.Id] = drops;
            }
            var bossDrops = new Dictionary<string, Dictionary<string, double>>();
            foreach (BossDef boss in content.Bosses.Values.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase))
                bossDrops[boss.Id] = boss.Rewards != null ? TableChances(content, boss.Rewards, 0) : new Dictionary<string, double>();

            var sources = new Dictionary<string, int>();
            foreach (Dictionary<string, double> drops in creatureDrops.Values.Concat(bossDrops.Values))
                foreach (string item in drops.Keys)
                    sources[item] = (sources.TryGetValue(item, out int n) ? n : 0) + 1;

            double rare = content.World.RareDropChance;
            var used = new HashSet<string>();
            bool elsewhere = false;
            foreach (NpcDef npc in creatures)
            {
                var entry = new BestiaryCreature
                {
                    Id = npc.Id,
                    Carcass = carcasses.TryGetValue(npc.Id, out BestiaryCarcass? carcass) ? carcass : null,
                    Notable = Notable(creatureDrops[npc.Id], sources, rare),
                };
                foreach (NpcSpawn spawn in spawns)
                {
                    if (spawn.Npc != npc.Id) continue;
                    WorldRegion? region = regionList.FirstOrDefault(r => r.Contains(spawn.X, spawn.Y));
                    if (region != null && !entry.Regions.Contains(region.Name)) entry.Regions.Add(region.Name);
                }
                foreach (string r in entry.Regions) used.Add(r);
                elsewhere |= entry.Regions.Count == 0;
                guide.Creatures.Add(entry);
            }
            foreach (KeyValuePair<string, Dictionary<string, double>> boss in bossDrops)
            {
                string region = content.Bosses[boss.Key].Region;
                guide.Bosses.Add(new BestiaryCreature
                {
                    Id = boss.Key,
                    Regions = string.IsNullOrEmpty(region) ? new List<string>() : new List<string> { region },
                    Notable = Notable(boss.Value, sources, rare),
                });
            }

            guide.Regions = regionList.Where(r => used.Contains(r.Name))
                .Select(r => new BestiaryRegion { Name = r.Name, Title = string.IsNullOrEmpty(r.Title) ? r.Name : r.Title })
                .OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (elsewhere) guide.Regions.Add(new BestiaryRegion { Name = "", Title = "Elsewhere" });
            return guide;
        }

        private static List<NotableDrop> Notable(Dictionary<string, double> drops, Dictionary<string, int> sources, double rareChance) =>
            drops.Select(d => new NotableDrop
                {
                    Item = d.Key,
                    Chance = d.Value,
                    Rare = d.Value <= rareChance,
                    Unique = d.Value < Guaranteed && sources.TryGetValue(d.Key, out int n) && n == 1,
                })
                .Where(d => d.Rare || d.Unique)
                .OrderByDescending(d => d.Chance).ThenBy(d => d.Item, StringComparer.Ordinal)
                .ToList();

        private const double Guaranteed = 0.999;

        /// <summary>
        /// Recipe id to the creature whose drop it works (its carcass): a recipe belongs to a
        /// creature if one of its inputs is something that creature's drop table gives.
        /// </summary>
        public static Dictionary<string, string> CarcassRecipes(GameContent content)
        {
            var map = new Dictionary<string, string>();
            var drops = new List<(string Npc, HashSet<string> Items)>();
            foreach (NpcDef npc in content.Npcs.Values.OrderBy(n => n.Id, StringComparer.Ordinal))
                if (npc.DropTable != null) drops.Add((npc.Id, new HashSet<string>(TableChances(content, npc.DropTable, 0).Keys)));
            foreach (RecipeDef recipe in content.Recipes.Values)
                foreach (ItemAmount input in recipe.Inputs)
                    foreach ((string npc, HashSet<string> items) in drops)
                        if (items.Contains(input.Item) && !map.ContainsKey(recipe.Id)) map[recipe.Id] = npc;
            return map;
        }

        /// <summary>
        /// Item id to the chance one roll of a table (its always entries and all its rolls,
        /// nested tables included) gives at least one of it.
        /// </summary>
        public static Dictionary<string, double> TableChances(GameContent content, string tableId, int depth)
        {
            var result = new Dictionary<string, double>();
            if (depth > MaxDepth || !content.DropTables.TryGetValue(tableId, out DropTableDef? table)) return result;
            foreach (DropEntry e in table.Always)
                foreach (KeyValuePair<string, double> c in EntryChances(content, e, depth)) Merge(result, c.Key, c.Value);

            int total = table.NothingWeight;
            foreach (DropEntry e in table.Entries) total += Math.Max(0, e.Weight);
            if (total <= 0 || table.Rolls <= 0) return result;
            // One roll picks one entry, so an item's chance on a roll is the sum over the entries that can give it.
            var perRoll = new Dictionary<string, double>();
            foreach (DropEntry e in table.Entries)
            {
                double pick = Math.Max(0, e.Weight) / (double)total;
                if (pick <= 0) continue;
                foreach (KeyValuePair<string, double> c in EntryChances(content, e, depth))
                    perRoll[c.Key] = Math.Min(1.0, (perRoll.TryGetValue(c.Key, out double had) ? had : 0) + pick * c.Value);
            }
            foreach (KeyValuePair<string, double> p in perRoll) Merge(result, p.Key, 1 - Math.Pow(1 - p.Value, table.Rolls));
            return result;
        }

        private static Dictionary<string, double> EntryChances(GameContent content, DropEntry entry, int depth)
        {
            if (entry.Table != null) return TableChances(content, entry.Table, depth + 1);
            var single = new Dictionary<string, double>();
            if (entry.Item != null && entry.Max > 0) single[entry.Item] = 1.0;
            return single;
        }

        /// <summary>Adds an independent chance of the same item: at least one of the two.</summary>
        private static void Merge(Dictionary<string, double> into, string item, double chance)
        {
            into[item] = into.TryGetValue(item, out double had) ? 1 - (1 - had) * (1 - chance) : chance;
        }
    }
}
