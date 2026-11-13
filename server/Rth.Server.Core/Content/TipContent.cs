using System.Collections.Generic;
using System.Linq;
using Rth.Shared.Combat;
using Rth.Shared.Items;

namespace Rth.Server.Content
{
    /// <summary>
    /// Tipped ammunition from tips.json: checks the tips, then makes, for every tip and every plain
    /// ammunition of a kind it fits, the tipped item ("copper_arrow.garnet"), the recipe that tips a
    /// batch (no station: tips used on the ammunition, either way round) and that use.
    /// </summary>
    internal static class TipContent
    {
        public static void Validate(TipsFile file, Dictionary<string, ItemDef> items, ICollection<string> skills, List<string> problems)
        {
            var seen = new HashSet<string>();
            foreach (TipDef t in file.Tips)
            {
                string where = "Tip '" + t.Id + "': ";
                if (string.IsNullOrWhiteSpace(t.Id) || !seen.Add(t.Id)) problems.Add(where + "needs a unique id.");
                if (t.Id.Contains('.')) problems.Add(where + "an id can't hold a dot (tipped items are named plain.tip).");
                if (!items.ContainsKey(t.Tip)) problems.Add(where + "tips item '" + t.Tip + "' doesn't exist.");
                if (!skills.Contains(t.Skill)) problems.Add(where + "skill '" + t.Skill + "' doesn't exist.");
                if (t.Ammo.Count == 0) problems.Add(where + "fits no kind of ammunition.");
                if (t.Batch < 1) problems.Add(where + "batch must be at least 1.");
                if (t.Chance < 0 || t.ChanceCalibrated < 0 || t.Chance + t.ChanceCalibrated > 100) problems.Add(where + "chances are percents, together at most 100.");
                TipEffect e = t.Effect;
                if (e.DamagePercent < 0 || e.HealPercent < 0 || e.BleedPercent < 0 || e.PinTicks < 0 || e.SplashRadius < 0 || e.SplashPercent < 0)
                    problems.Add(where + "effect numbers can't be negative.");
                if (e.BleedPercent > 0 && e.BleedTicks < 1) problems.Add(where + "a bleed needs bleedTicks of at least 1.");
                if ((e.SplashRadius > 0) != (e.SplashPercent > 0)) problems.Add(where + "a splash needs both splashRadius and splashPercent.");
                if (e.DamagePercent + e.HealPercent + e.BleedPercent + e.PinTicks + e.SplashPercent == 0) problems.Add(where + "its effect does nothing.");
                foreach (string kind in t.Ammo)
                    if (!items.Values.Any(i => i.Ammo != null && i.Ammo.Kind == kind && i.Ammo.Tip == null))
                        problems.Add(where + "no ammunition of kind '" + kind + "' to tip.");
            }
        }

        /// <summary>Adds the tipped items to <paramref name="items"/>; returns the recipes and uses that make them.</summary>
        public static (List<RecipeDef> Recipes, List<UseDef> Uses) Generate(TipsFile file, Dictionary<string, ItemDef> items)
        {
            var recipes = new List<RecipeDef>();
            var uses = new List<UseDef>();
            List<ItemDef> plain = items.Values.Where(i => i.Ammo != null && i.Ammo.Tip == null).ToList();
            foreach (TipDef t in file.Tips)
            {
                ItemDef tips = items[t.Tip];
                foreach (ItemDef ammo in plain.Where(a => t.Ammo.Contains(a.Ammo!.Kind)))
                {
                    string id = ammo.Id + "." + t.Id;
                    if (items.ContainsKey(id)) continue; // written by hand: theirs wins
                    string name = t.Name + "-tipped " + Lower(ammo.Name);
                    items[id] = new ItemDef
                    {
                        Id = id,
                        Name = name,
                        Examine = ammo.Examine + " Tipped with " + Lower(tips.Name) + ": " + Lower(t.Effect.Name) + ".",
                        Stackable = ammo.Stackable,
                        Value = ammo.Value + tips.Value,
                        Color = tips.Color,
                        Icon = ammo.Icon,
                        Equipment = ammo.Equipment,
                        Ammo = new AmmoDef { Kind = ammo.Ammo!.Kind, Tier = ammo.Ammo.Tier, Tip = t.Id, Plain = ammo.Id },
                        Tags = ammo.Tags.Concat(new[] { "tipped" }).Distinct().ToList(),
                    };
                    string recipe = "tip_" + id;
                    recipes.Add(new RecipeDef
                    {
                        Id = recipe,
                        Name = Lower(name) + "s",
                        Verb = "tip",
                        Skill = t.Skill,
                        Level = t.Level,
                        Group = "Tipping",
                        Inputs = { new ItemAmount { Item = ammo.Id, Count = t.Batch }, new ItemAmount { Item = t.Tip, Count = t.Batch } },
                        Outputs = { new ItemAmount { Item = id, Count = t.Batch } },
                        Ticks = 2,
                        Xp = t.Xp,
                    });
                    uses.Add(new UseDef { Id = recipe, Item = t.Tip, OnItem = ammo.Id, Recipe = recipe });
                }
            }
            return (recipes, uses);
        }

        private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);
    }
}
