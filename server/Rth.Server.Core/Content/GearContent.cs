using System;
using System.Collections.Generic;
using System.Linq;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Skills;

namespace Rth.Server.Content
{
    /// <summary>
    /// Gear stats by tier (docs/skills.md 2.2), from gear.json: a tier-N item of a family gets the
    /// tier's base value times its slot's share of each stat, so a new item needs only a tier, a
    /// family and a slot. A family can also ask for a skill at the tier's level (2.5: plate needs
    /// Defence, swords Melee, a tier-N piece level (N - 1) x 10). Stats an item lists itself are
    /// added on top. The numbers are provisional, for tuning by play.
    /// </summary>
    public sealed class GearRules
    {
        /// <summary>The tier-1 base value.</summary>
        public double Base = 10;
        /// <summary>Added to the base for every tier above 1.</summary>
        public double PerTier = 6;
        /// <summary>Levels between tiers (2.2: one tier per ten levels).</summary>
        public int LevelsPerTier = 10;
        public Dictionary<string, GearFamily> Families = new Dictionary<string, GearFamily>();

        /// <summary>Equipment already filled in, so building content twice from the same parts (tests) doesn't add twice.</summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EquipmentDef, object> Applied =
            new System.Runtime.CompilerServices.ConditionalWeakTable<EquipmentDef, object>();

        public double BaseFor(int tier) => Base + PerTier * Math.Max(0, tier - 1);

        /// <summary>The level a tier asks for: 1 at tier 1, 10 at tier 2, 20 at tier 3...</summary>
        public int LevelFor(int tier) => Math.Max(1, (tier - 1) * LevelsPerTier);

        /// <summary>
        /// Fills in the stats and requirement of every item with a gear family and tier. Returns the
        /// problems (unknown family, a slot the family doesn't cover, an unknown stat or skill).
        /// </summary>
        public List<string> Apply(IEnumerable<ItemDef> items, ICollection<string> skills)
        {
            var problems = new List<string>();
            foreach (KeyValuePair<string, GearFamily> f in Families)
            {
                foreach (Dictionary<string, double> shares in f.Value.Slots.Values)
                    foreach (string stat in shares.Keys)
                        if (!StatIds.IsKnown(stat)) problems.Add("Gear family '" + f.Key + "': unknown stat '" + stat + "'.");
                if (f.Value.Requires != null && !skills.Contains(f.Value.Requires)) problems.Add("Gear family '" + f.Key + "': requires unknown skill '" + f.Value.Requires + "'.");
            }
            foreach (ItemDef item in items)
            {
                EquipmentDef? e = item.Equipment;
                if (e?.Family == null) continue;
                string where = "Item '" + item.Id + "': ";
                if (!Families.TryGetValue(e.Family, out GearFamily? family))
                {
                    problems.Add(where + "gear family '" + e.Family + "' isn't in gear.json.");
                    continue;
                }
                if (e.Tier < 1) problems.Add(where + "a gear family needs a tier of at least 1.");
                if (!family.Slots.TryGetValue(e.Slot.ToString(), out Dictionary<string, double>? shares))
                {
                    problems.Add(where + "gear family '" + e.Family + "' has no " + e.Slot + " slot.");
                    continue;
                }
                if (Applied.TryGetValue(e, out _)) continue;
                Applied.Add(e, family);
                double value = BaseFor(e.Tier);
                foreach (KeyValuePair<string, double> share in shares)
                {
                    int add = (int)Math.Round(value * share.Value, MidpointRounding.AwayFromZero);
                    if (add == 0) continue;
                    e.Stats[share.Key] = (e.Stats.TryGetValue(share.Key, out int had) ? had : 0) + add;
                }
                if (family.Requires != null && e.Tier > 1 && !e.Requirements.Any(r => r.Skill == family.Requires))
                    e.Requirements.Add(new SkillRequirement { Skill = family.Requires, Level = LevelFor(e.Tier) });
            }
            return problems;
        }
    }

    /// <summary>A family of gear (plate, one-handed melee weapons): the skill it needs, and each slot's share of each stat.</summary>
    public sealed class GearFamily
    {
        /// <summary>The skill a tier-N piece needs at the tier's level (defence for armour, the style for weapons), or null.</summary>
        public string? Requires;
        /// <summary>Slot name (EquipSlot) to stat id to its share of the tier's base value.</summary>
        public Dictionary<string, Dictionary<string, double>> Slots = new Dictionary<string, Dictionary<string, double>>();
    }
}
