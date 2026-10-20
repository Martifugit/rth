using System;
using System.Collections.Generic;

namespace Rth.Server.Content
{
    /// <summary>
    /// A loot table, from drops.json. Every <see cref="Always"/> entry drops; then
    /// <see cref="Rolls"/> weighted picks are made from <see cref="Entries"/> (plus "nothing").
    /// An entry can name another table, so tables nest and can be shared (a rare table many
    /// creatures roll on). Not just for creatures: carcasses, chests and skilling can use them too.
    /// </summary>
    public sealed class DropTableDef
    {
        public string Id = "";
        public List<DropEntry> Always = new List<DropEntry>();
        public int Rolls = 1;
        public List<DropEntry> Entries = new List<DropEntry>();
        /// <summary>Weight of rolling nothing at all.</summary>
        public int NothingWeight;
    }

    public sealed class DropEntry
    {
        /// <summary>An item id, or null if this entry rolls on <see cref="Table"/>.</summary>
        public string? Item;
        /// <summary>A nested table id, or null.</summary>
        public string? Table;
        public int Min = 1;
        public int Max = 1;
        public int Weight = 1;
    }

    public readonly struct Drop
    {
        public Drop(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }

        public string ItemId { get; }
        public int Count { get; }
    }

    public static class DropRoller
    {
        private const int MaxDepth = 8;

        public static List<Drop> Roll(GameContent content, string tableId, Random random)
        {
            var drops = new List<Drop>();
            Roll(content, tableId, random, drops, 0);
            return drops;
        }

        private static void Roll(GameContent content, string tableId, Random random, List<Drop> drops, int depth)
        {
            if (depth > MaxDepth || !content.DropTables.TryGetValue(tableId, out DropTableDef? table)) return;
            foreach (DropEntry entry in table.Always) Give(content, entry, random, drops, depth);

            int total = table.NothingWeight;
            foreach (DropEntry e in table.Entries) total += Math.Max(0, e.Weight);
            if (total <= 0) return;

            for (int roll = 0; roll < table.Rolls; roll++)
            {
                int pick = random.Next(total);
                foreach (DropEntry e in table.Entries)
                {
                    pick -= Math.Max(0, e.Weight);
                    if (pick < 0)
                    {
                        Give(content, e, random, drops, depth);
                        break;
                    }
                }
                // Falling through every entry means the "nothing" weight was rolled.
            }
        }

        private static void Give(GameContent content, DropEntry entry, Random random, List<Drop> drops, int depth)
        {
            if (entry.Table != null)
            {
                Roll(content, entry.Table, random, drops, depth + 1);
                return;
            }
            if (entry.Item == null) return;
            int count = random.Next(entry.Min, entry.Max + 1);
            if (count > 0) drops.Add(new Drop(entry.Item, count));
        }
    }
}
