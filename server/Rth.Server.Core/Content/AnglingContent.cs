using System.Collections.Generic;
using System.Linq;
using Rth.Shared.Angling;
using Rth.Shared.Items;
using Rth.Shared.Skills;
using Rth.Shared.World;

namespace Rth.Server.Content
{
    /// <summary>
    /// angling.json's checks, and the items it makes for every species: a trophy for each size
    /// class and a revised one, and a fish box for each (docs/fishing.md 5, 6).
    /// </summary>
    public static class AnglingContent
    {
        public const string CastAction = "cast";

        public static string TrophyId(string species, string sizeClass) => "trophy_" + species + "_" + sizeClass;
        public static string BoxId(string species, string sizeClass) => "fishbox_" + species + "_" + sizeClass;
        public const string RevisedClass = "revised";
        public const string TrophyTag = "trophy";

        public static void Validate(AnglingFile file, Dictionary<string, ItemDef> items, Dictionary<string, ObjectDef> objects,
            HashSet<string> skills, List<string> problems)
        {
            if (file.Species.Count > 0 && !skills.Contains(file.Skill)) problems.Add("Angling: skill '" + file.Skill + "' doesn't exist.");
            if (file.Species.Count > 0 && file.SizeClasses.Count == 0) problems.Add("Angling: species need size classes.");
            int last = -1;
            var classIds = new HashSet<string>();
            foreach (AnglingSizeClass c in file.SizeClasses)
            {
                if (string.IsNullOrWhiteSpace(c.Id) || c.Id == RevisedClass || !classIds.Add(c.Id)) problems.Add("Angling: size class ids must be unique, not empty and not '" + RevisedClass + "'.");
                if (c.UpTo <= last) problems.Add("Angling: size class '" + c.Id + "' must run higher than the one before.");
                if (c.BoxRolls < 1) problems.Add("Angling: size class '" + c.Id + "' needs boxRolls of at least 1.");
                last = c.UpTo;
            }
            if (file.SizeClasses.Count > 0 && last <= 1000) problems.Add("Angling: the largest size class must run up to 1001.");
            if (file.RevisedChance < 0 || file.RevisedChance > 1) problems.Add("Angling: revisedChance must be 0-1.");
            if (file.BiteMinTicks < 1 || file.BiteMaxTicks < file.BiteMinTicks) problems.Add("Angling: biteMinTicks must be at least 1, and biteMaxTicks at least that.");
            if (file.WornPercent < 0 || file.WornPercent > 100) problems.Add("Angling: wornPercent must be 0-100.");

            var ids = new HashSet<string>();
            foreach (AnglingSpeciesDef s in file.Species)
            {
                string where = "Angling species '" + s.Id + "': ";
                if (string.IsNullOrWhiteSpace(s.Id) || !ids.Add(s.Id)) problems.Add("Angling: species ids must be unique and not empty ('" + s.Id + "').");
                if (string.IsNullOrWhiteSpace(s.Name)) problems.Add(where + "needs a name.");
                if (!objects.TryGetValue(s.SpotType, out ObjectDef? spot)) problems.Add(where + "spot type '" + s.SpotType + "' isn't an object.");
                else if (!spot.Actions.Any(a => a.Id == CastAction)) problems.Add(where + "its spot type '" + s.SpotType + "' needs a '" + CastAction + "' action.");
                if (file.Species.Count(o => o.SpotType == s.SpotType) > 1) problems.Add(where + "its spot type is another species' too.");
                if (s.Active < 1 || s.ActiveMinTicks < 1 || s.ActiveMaxTicks < s.ActiveMinTicks) problems.Add(where + "active, activeMinTicks must be at least 1 and activeMaxTicks at least activeMinTicks.");
                if (s.SpookAfter < 1 || s.SpookTicks < 1) problems.Add(where + "spookAfter and spookTicks must be at least 1.");
                if (s.MinGrams < 1 || s.MaxGrams < s.MinGrams) problems.Add(where + "needs minGrams of at least 1 and maxGrams at least minGrams.");
                if (s.Xp < 0) problems.Add(where + "xp can't be negative.");
                AnglingFightDef f = s.Fight;
                if (f.Energy < 1 || f.Distance < 1 || f.TellSteps < 1 || f.MoveMinSteps < 1 || f.MoveMaxSteps < f.MoveMinSteps || f.BreatherSteps < 0)
                    problems.Add(where + "its fight needs energy, distance, tellSteps and moveMinSteps of at least 1, and moveMaxSteps at least moveMinSteps.");
                if (f.Turn + f.Dive + f.Run + f.Jump + f.Latch <= 0 || f.Turn < 0 || f.Dive < 0 || f.Run < 0 || f.Jump < 0 || f.Latch < 0)
                    problems.Add(where + "its fight's move weights can't be negative and must add up to more than 0.");
                foreach (OpenEntry e in s.Box)
                    if (!items.ContainsKey(e.Item) || e.Min < 1 || e.Max < e.Min || e.Weight < 1) problems.Add(where + "bad box entry '" + e.Item + "'.");
                if (s.Box.Count == 0) problems.Add(where + "its fish box holds nothing.");
                var notes = new HashSet<string>();
                foreach (AnglingNoteDef n in s.Notes)
                    if (string.IsNullOrWhiteSpace(n.Id) || !notes.Add(n.Id) || string.IsNullOrWhiteSpace(n.Text)) problems.Add(where + "notes need unique ids and text.");
            }

            foreach (ItemDef i in items.Values)
            {
                AnglingTackleDef? t = i.Tackle;
                if (t == null) continue;
                string where = "Item '" + i.Id + "' (tackle): ";
                if (i.Stackable) problems.Add(where + "tackle can't be stackable.");
                foreach (SkillRequirement r in t.Requirements.Concat(t.Repair?.Requirements ?? new List<SkillRequirement>()))
                    if (!skills.Contains(r.Skill)) problems.Add(where + "requires unknown skill '" + r.Skill + "'.");
                foreach (ItemCost m in t.Repair?.Materials ?? new List<ItemCost>())
                    if (!items.ContainsKey(m.Item) || m.Count < 1) problems.Add(where + "bad repair material '" + m.Item + "'.");
                foreach (string b in t.Bites)
                    if (!b.StartsWith("tier:") && !file.Species.Any(s => s.Id == b)) problems.Add(where + "bites names unknown species '" + b + "'.");
                if (t.Slot == AnglingSlot.Lure && t.Bites.Count == 0) problems.Add(where + "a lure needs something to bite on it.");
                if (t.WearPerFight < 0 || t.WearPerHard < 0) problems.Add(where + "wear can't be negative.");
            }
            foreach (ItemDef i in items.Values)
            {
                if (i.Open == null) continue;
                if (i.Open.Rolls < 1 || i.Open.Entries.Count == 0) problems.Add("Item '" + i.Id + "': open needs rolls of at least 1 and entries.");
                foreach (OpenEntry e in i.Open.Entries)
                    if (!items.ContainsKey(e.Item) || e.Min < 1 || e.Max < e.Min || e.Weight < 1) problems.Add("Item '" + i.Id + "': bad open entry '" + e.Item + "'.");
            }
        }

        /// <summary>Adds each species' trophies and fish boxes to the items (none that already exist are replaced).</summary>
        public static void Generate(AnglingFile file, Dictionary<string, ItemDef> items)
        {
            foreach (AnglingSpeciesDef s in file.Species)
            {
                var classes = file.SizeClasses.Select(c => (c.Id, c.Name, c.BoxRolls)).ToList();
                classes.Add((RevisedClass, "revised", file.RevisedBoxRolls));
                foreach (var (id, name, rolls) in classes)
                {
                    bool revised = id == RevisedClass;
                    string trophy = TrophyId(s.Id, id), box = BoxId(s.Id, id);
                    if (!items.ContainsKey(trophy))
                        items[trophy] = new ItemDef
                        {
                            Id = trophy,
                            Name = revised ? "Revised " + s.Name.ToLowerInvariant() : s.Name + " (" + name.ToLowerInvariant() + ")",
                            Examine = revised
                                ? "A " + s.Name.ToLowerInvariant() + ", but not quite: something is still being decided about it. A collector of such things would want it."
                                : "A fine " + s.Name.ToLowerInvariant() + " for any fish market's slab.",
                            Value = (s.Tier * 20) * (revised ? 10 : 1),
                            Color = s.Color,
                            Icon = s.Icon,
                            Tags = new List<string> { TrophyTag, TrophyTag + ":" + s.Id },
                        };
                    if (!items.ContainsKey(box))
                        items[box] = new ItemDef
                        {
                            Id = box,
                            Name = "Fish box (" + s.Name.ToLowerInvariant() + ", " + name.ToLowerInvariant() + ")",
                            Examine = "A slatted box packed with fish and wet straw.",
                            Value = s.Tier * 10 * rolls,
                            Color = "#8a7350",
                            Icon = "pouch",
                            Tags = new List<string> { "fishbox" },
                            Open = new OpenDef { Verb = "Open", Rolls = rolls, Entries = s.Box },
                        };
                }
            }
        }
    }
}
