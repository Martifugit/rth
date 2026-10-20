using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Skills;
using Rth.Shared.World;

namespace Rth.Server.Content
{
    /// <summary>A creature type, from npcs.json.</summary>
    public sealed class NpcDef
    {
        public string Id = "";
        public string Name = "";
        public int Blood = 100;
        public CombatStyle Style = CombatStyle.Melee;
        /// <summary>False for things that never fight back, like training effigies.</summary>
        public bool Attacks = true;
        /// <summary>1 = melee (orthogonally adjacent); more = ranged/occult in tiles.</summary>
        public int AttackRange = 1;
        public int AttackTicks = 4;
        public int MaxHit = 10;
        /// <summary>Chance its attacks land.</summary>
        public double Accuracy = 0.7;
        /// <summary>Taken off the attacker's accuracy.</summary>
        public double Evasion = 0.1;
        public bool Aggressive;
        public int AggroRadius = 3;
        /// <summary>Gives up and walks home when dragged this far from its spawn.</summary>
        public int LeashRadius = 12;
        public int WanderRadius = 3;
        public int RespawnTicks = 30;
        /// <summary>Heals to full as soon as it's out of combat.</summary>
        public bool Regenerates;
        /// <summary>Drop table rolled when it dies (drops.json), or null for none.</summary>
        public string? DropTable;

        public EntityShape Shape = EntityShape.Capsule;
        public string Color = "#888888";
        public double Scale = 1.0;

        public int ColorRgb => int.Parse(Color.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    /// <summary>Lines the Assayer says when a player dies, by situation. From assayer.json.</summary>
    public sealed class AssayerLines
    {
        public List<string> First = new List<string>();
        public List<string> Choleric = new List<string>();
        public List<string> Melancholic = new List<string>();
        public List<string> Balanced = new List<string>();
        public List<string> Frequent = new List<string>();
    }

    /// <summary>Where a creature lives, exported with the map (<c>&lt;map&gt;.spawns.json</c>).</summary>
    public sealed class NpcSpawn
    {
        public string Npc = "";
        public int Plane;
        public int X;
        public int Y;
        /// <summary>Overrides the creature's wander radius when 0 or more.</summary>
        public int Wander = -1;
    }

    public sealed class ItemAmount
    {
        public string Item = "";
        public int Count = 1;
    }

    /// <summary>World-wide settings that aren't combat, from world.json.</summary>
    public sealed class WorldSettings
    {
        public int InventorySize = 28;
        /// <summary>Loot and dropped items are only visible to their owner for this long.</summary>
        public int GroundItemPrivateTicks = 100;
        /// <summary>Ground items vanish this long after appearing.</summary>
        public int GroundItemDespawnTicks = 300;
        public List<ItemAmount> StarterInventory = new List<ItemAmount>();
        public List<string> StarterEquipment = new List<string>();
    }

    /// <summary>
    /// Making something at a station: a skill action that repeats (semi-AFK) while you have the
    /// inputs. Butchering carcasses now; distilling, brewing and cooking later. From recipes.json.
    /// </summary>
    public sealed class RecipeDef
    {
        public string Id = "";
        /// <summary>What's being worked on, for messages ("fen rat carcass").</summary>
        public string Name = "";
        /// <summary>The verb for messages ("butcher").</summary>
        public string Verb = "process";
        public string Skill = "";
        public int Level = 1;
        /// <summary>Object type it's done at.</summary>
        public string Station = "";
        public List<ItemAmount> Inputs = new List<ItemAmount>();
        /// <summary>Items needed in the pack or worn, not used up.</summary>
        public List<string> Tools = new List<string>();
        public List<ItemAmount> Outputs = new List<ItemAmount>();
        /// <summary>A drop table rolled for each completion, or null.</summary>
        public string? OutputTable;
        /// <summary>One extra roll of the output table per this many levels above <see cref="Level"/> (0 = never).</summary>
        public int ExtraRollEveryLevels;
        public int Ticks = 4;
        public double Xp;
    }

    /// <summary>Damage dealt with a combat style trains a skill.</summary>
    public sealed class DamageXpRule
    {
        public CombatStyle Style;
        public string Skill = "";
        public double XpPerDamage;
    }

    public sealed class SkillsFile
    {
        public XpCurve XpCurve = new XpCurve();
        public List<SkillDef> Skills = new List<SkillDef>();
        public List<DamageXpRule> DamageXp = new List<DamageXpRule>();
    }

    public sealed class ContentException : Exception
    {
        public ContentException(string message) : base(message) { }
    }

    /// <summary>The raw pieces content is built from: one field per kind of content. Add a field for a new kind.</summary>
    public sealed class ContentParts
    {
        public CombatRules Rules = new CombatRules();
        public List<AbilityDef> Abilities = new List<AbilityDef>();
        public string AutoAttack = "";
        public List<string> ActionBar = new List<string>();
        public List<NpcDef> Npcs = new List<NpcDef>();
        public AssayerLines Assayer = new AssayerLines();
        public List<ItemDef> Items = new List<ItemDef>();
        public List<DropTableDef> DropTables = new List<DropTableDef>();
        public WorldSettings World = new WorldSettings();
        public SkillsFile Skills = new SkillsFile();
        public List<ObjectDef> Objects = new List<ObjectDef>();
        public List<RecipeDef> Recipes = new List<RecipeDef>();
    }

    /// <summary>
    /// Everything loaded from server/data/content. Immutable once loaded: a hot reload builds a new
    /// one and the world swaps it in between ticks.
    /// </summary>
    public sealed class GameContent
    {
        private sealed class CombatFile
        {
            public CombatRules Rules = new CombatRules();
            public string AutoAttack = "";
            public List<string> ActionBar = new List<string>();
        }

        public CombatRules Rules { get; private set; } = new CombatRules();
        public IReadOnlyDictionary<string, AbilityDef> Abilities { get; private set; } = new Dictionary<string, AbilityDef>();
        public AbilityDef AutoAttack { get; private set; } = new AbilityDef();
        public IReadOnlyList<string> ActionBar { get; private set; } = new List<string>();
        public IReadOnlyDictionary<string, NpcDef> Npcs { get; private set; } = new Dictionary<string, NpcDef>();
        public AssayerLines Assayer { get; private set; } = new AssayerLines();
        public IReadOnlyDictionary<string, ItemDef> Items { get; private set; } = new Dictionary<string, ItemDef>();
        public IReadOnlyDictionary<string, DropTableDef> DropTables { get; private set; } = new Dictionary<string, DropTableDef>();
        public WorldSettings World { get; private set; } = new WorldSettings();
        public XpCurve XpCurve { get; private set; } = new XpCurve();
        public IReadOnlyDictionary<string, SkillDef> Skills { get; private set; } = new Dictionary<string, SkillDef>();
        public IReadOnlyList<DamageXpRule> DamageXp { get; private set; } = new List<DamageXpRule>();
        public IReadOnlyDictionary<string, ObjectDef> Objects { get; private set; } = new Dictionary<string, ObjectDef>();
        public IReadOnlyDictionary<string, RecipeDef> Recipes { get; private set; } = new Dictionary<string, RecipeDef>();

        public ItemDef? Item(string? id) => id != null && Items.TryGetValue(id, out ItemDef? def) ? def : null;

        public string SkillName(string id) => Skills.TryGetValue(id, out SkillDef? s) ? s.Name : id;

        public static GameContent Load(string directory)
        {
            var combat = ReadJson<CombatFile>(directory, "combat.json");
            return Create(new ContentParts
            {
                Rules = combat.Rules,
                AutoAttack = combat.AutoAttack,
                ActionBar = combat.ActionBar,
                Abilities = ReadJson<List<AbilityDef>>(directory, "abilities.json"),
                Npcs = ReadJson<List<NpcDef>>(directory, "npcs.json"),
                Assayer = ReadJson<AssayerLines>(directory, "assayer.json"),
                Items = ReadJson<List<ItemDef>>(directory, "items.json"),
                DropTables = ReadJson<List<DropTableDef>>(directory, "drops.json"),
                World = ReadJson<WorldSettings>(directory, "world.json"),
                Skills = ReadJson<SkillsFile>(directory, "skills.json"),
                Objects = ReadJson<List<ObjectDef>>(directory, "objects.json"),
                Recipes = ReadJson<List<RecipeDef>>(directory, "recipes.json"),
            });
        }

        /// <summary>Builds and validates content; throws <see cref="ContentException"/> listing every problem.</summary>
        public static GameContent Create(ContentParts parts)
        {
            var problems = new List<string>();

            Dictionary<string, AbilityDef> abilities = Index(parts.Abilities, a => a.Id, "ability", problems);
            foreach (AbilityDef a in parts.Abilities)
            {
                if (a.DamageMin < 0 || a.DamageMax < a.DamageMin) problems.Add("Ability '" + a.Id + "': damage range is invalid.");
                if (a.Range < 1) problems.Add("Ability '" + a.Id + "': range must be at least 1.");
                if (a.CooldownTicks < 0) problems.Add("Ability '" + a.Id + "': negative cooldown.");
                if (a.BileMin > a.BileMax) problems.Add("Ability '" + a.Id + "': bileMin is above bileMax.");
                if (a.PhlegmCost > a.PhlegmRequired && a.Type != AbilityType.Basic) problems.Add("Ability '" + a.Id + "': costs more phlegm than it requires.");
            }

            if (!abilities.TryGetValue(parts.AutoAttack ?? "", out AbilityDef? auto)) problems.Add("autoAttack '" + parts.AutoAttack + "' is not an ability.");
            else if (auto.Type != AbilityType.Auto) problems.Add("autoAttack '" + parts.AutoAttack + "' must have type Auto.");

            foreach (string id in parts.ActionBar)
            {
                if (!abilities.TryGetValue(id, out AbilityDef? a)) problems.Add("Action bar ability '" + id + "' doesn't exist.");
                else if (a.Type == AbilityType.Auto) problems.Add("Action bar ability '" + id + "' is an auto-attack.");
            }

            Dictionary<string, ItemDef> items = Index(parts.Items, i => i.Id, "item", problems);
            foreach (ItemDef item in parts.Items) ValidateItem(item, items, problems);

            Dictionary<string, DropTableDef> tables = Index(parts.DropTables, t => t.Id, "drop table", problems);
            foreach (DropTableDef table in parts.DropTables) ValidateTable(table, items, tables, problems);

            Dictionary<string, NpcDef> npcs = Index(parts.Npcs, n => n.Id, "NPC", problems);
            foreach (NpcDef n in parts.Npcs)
            {
                if (n.Blood <= 0) problems.Add("NPC '" + n.Id + "': blood must be positive.");
                if (n.AttackRange < 1 || n.AttackTicks < 1) problems.Add("NPC '" + n.Id + "': attackRange and attackTicks must be at least 1.");
                if (n.DropTable != null && !tables.ContainsKey(n.DropTable)) problems.Add("NPC '" + n.Id + "': drop table '" + n.DropTable + "' doesn't exist.");
                if (!IsColor(n.Color)) problems.Add("NPC '" + n.Id + "': color '" + n.Color + "' isn't #RRGGBB.");
            }

            foreach (ItemAmount start in parts.World.StarterInventory)
                if (!items.ContainsKey(start.Item)) problems.Add("Starter item '" + start.Item + "' doesn't exist.");
            foreach (string id in parts.World.StarterEquipment)
                if (!items.TryGetValue(id, out ItemDef? e) || e.Equipment == null) problems.Add("Starter equipment '" + id + "' isn't an equippable item.");
            if (parts.World.InventorySize < 1 || parts.World.InventorySize > 255) problems.Add("World: inventorySize must be 1-255.");

            Dictionary<string, SkillDef> skills = Index(parts.Skills.Skills, s => s.Id, "skill", problems);
            foreach (SkillDef s in parts.Skills.Skills)
                foreach (string stat in s.StatsPerLevel.Keys)
                    if (!StatIds.IsKnown(stat)) problems.Add("Skill '" + s.Id + "': unknown stat '" + stat + "'.");
            XpCurve curve = parts.Skills.XpCurve;
            if (curve.MaxLevel < 2 || curve.FirstLevelXp <= 0 || curve.Growth < 1) problems.Add("XP curve: need maxLevel >= 2, firstLevelXp > 0, growth >= 1.");
            foreach (DamageXpRule rule in parts.Skills.DamageXp)
                if (!skills.ContainsKey(rule.Skill)) problems.Add("Damage XP rule for " + rule.Style + ": skill '" + rule.Skill + "' doesn't exist.");

            void CheckRequirements(IEnumerable<SkillRequirement> reqs, string where)
            {
                foreach (SkillRequirement r in reqs)
                    if (!skills.ContainsKey(r.Skill)) problems.Add(where + ": requires unknown skill '" + r.Skill + "'.");
            }
            foreach (AbilityDef a in parts.Abilities) CheckRequirements(a.Requirements, "Ability '" + a.Id + "'");
            foreach (ItemDef i in parts.Items)
                if (i.Equipment != null) CheckRequirements(i.Equipment.Requirements, "Item '" + i.Id + "'");

            Dictionary<string, ObjectDef> objects = Index(parts.Objects, o => o.Id, "object", problems);
            Dictionary<string, RecipeDef> recipes = Index(parts.Recipes, r => r.Id, "recipe", problems);
            foreach (RecipeDef r in parts.Recipes)
            {
                string where = "Recipe '" + r.Id + "': ";
                if (!skills.ContainsKey(r.Skill)) problems.Add(where + "skill '" + r.Skill + "' doesn't exist.");
                if (!objects.ContainsKey(r.Station)) problems.Add(where + "station '" + r.Station + "' isn't an object.");
                if (r.Inputs.Count == 0) problems.Add(where + "needs at least one input.");
                foreach (ItemAmount a in r.Inputs.Concat(r.Outputs))
                    if (!items.ContainsKey(a.Item) || a.Count < 1) problems.Add(where + "bad item '" + a.Item + "' x" + a.Count + ".");
                foreach (string tool in r.Tools)
                    if (!items.ContainsKey(tool)) problems.Add(where + "tool '" + tool + "' doesn't exist.");
                if (r.OutputTable != null && !tables.ContainsKey(r.OutputTable)) problems.Add(where + "output table '" + r.OutputTable + "' doesn't exist.");
                if (r.Ticks < 1) problems.Add(where + "ticks must be at least 1.");
            }

            CombatRules rules = parts.Rules;
            if (rules.BilePassiveThreshold <= 0 || rules.BileExtreme <= rules.BilePassiveThreshold || rules.BileLimit < rules.BileExtreme)
                problems.Add("Rules: need 0 < bilePassiveThreshold < bileExtreme <= bileLimit.");

            if (problems.Count > 0) throw new ContentException(string.Join(Environment.NewLine, problems));

            return new GameContent
            {
                Rules = rules,
                Abilities = abilities,
                AutoAttack = auto!,
                ActionBar = parts.ActionBar.ToList(),
                Npcs = npcs,
                Assayer = parts.Assayer ?? new AssayerLines(),
                Items = items,
                DropTables = tables,
                World = parts.World,
                XpCurve = curve,
                Skills = skills,
                DamageXp = parts.Skills.DamageXp,
                Objects = objects,
                Recipes = recipes,
            };
        }

        private static readonly HashSet<string> KnownEffectOps = new HashSet<string>
        {
            EffectOp.HealBlood, EffectOp.HealBloodPercent, EffectOp.RestoreBreath, EffectOp.AddPhlegm,
            EffectOp.ShiftBile, EffectOp.SettleBile, EffectOp.ApplyEffect,
        };

        private static void ValidateItem(ItemDef item, Dictionary<string, ItemDef> items, List<string> problems)
        {
            string where = "Item '" + item.Id + "': ";
            if (string.IsNullOrWhiteSpace(item.Name)) problems.Add(where + "has no name.");
            if (!IsColor(item.Color)) problems.Add(where + "color '" + item.Color + "' isn't #RRGGBB.");
            if (item.Equipment != null)
            {
                foreach (string stat in item.Equipment.Stats.Keys)
                    if (!StatIds.IsKnown(stat)) problems.Add(where + "unknown stat '" + stat + "' (known: " + string.Join(", ", StatIds.All) + ").");
                if (item.Stackable) problems.Add(where + "equipment can't be stackable (yet).");
            }
            if (item.Consumable != null)
            {
                foreach (EffectOp op in item.Consumable.Effects)
                    if (!KnownEffectOps.Contains(op.Op)) problems.Add(where + "unknown effect op '" + op.Op + "'.");
                if (item.Consumable.Leaves != null && !items.ContainsKey(item.Consumable.Leaves))
                    problems.Add(where + "leaves '" + item.Consumable.Leaves + "', which doesn't exist.");
            }
        }

        private static void ValidateTable(DropTableDef table, Dictionary<string, ItemDef> items, Dictionary<string, DropTableDef> tables, List<string> problems)
        {
            string where = "Drop table '" + table.Id + "': ";
            foreach (DropEntry e in table.Always.Concat(table.Entries))
            {
                if ((e.Item == null) == (e.Table == null)) problems.Add(where + "each entry needs exactly one of item or table.");
                if (e.Item != null && !items.ContainsKey(e.Item)) problems.Add(where + "item '" + e.Item + "' doesn't exist.");
                if (e.Table != null && !tables.ContainsKey(e.Table)) problems.Add(where + "table '" + e.Table + "' doesn't exist.");
                if (e.Min < 0 || e.Max < e.Min) problems.Add(where + "bad count range " + e.Min + "-" + e.Max + ".");
                if (e.Weight < 0) problems.Add(where + "negative weight.");
            }
            if (table.Rolls < 0 || table.NothingWeight < 0) problems.Add(where + "rolls and nothingWeight can't be negative.");
            if (ReachesItself(table.Id, table.Id, tables, new HashSet<string>())) problems.Add(where + "refers back to itself.");
        }

        private static bool ReachesItself(string start, string current, Dictionary<string, DropTableDef> tables, HashSet<string> visited)
        {
            if (!visited.Add(current) || !tables.TryGetValue(current, out DropTableDef? table)) return false;
            foreach (DropEntry e in table.Always.Concat(table.Entries))
            {
                if (e.Table == null) continue;
                if (e.Table == start || ReachesItself(start, e.Table, tables, visited)) return true;
            }
            return false;
        }

        private static Dictionary<string, T> Index<T>(IEnumerable<T> things, Func<T, string> id, string kind, List<string> problems)
        {
            var map = new Dictionary<string, T>();
            foreach (T thing in things)
            {
                string key = id(thing);
                if (string.IsNullOrWhiteSpace(key)) problems.Add("A" + (kind[0] == 'i' || kind[0] == 'a' ? "n " : " ") + kind + " has no id.");
                else if (map.ContainsKey(key)) problems.Add("Duplicate " + kind + " id '" + key + "'.");
                else map.Add(key, thing);
            }
            return map;
        }

        private static bool IsColor(string color) =>
            color != null && color.Length == 7 && color[0] == '#' &&
            int.TryParse(color.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);

        public static List<NpcSpawn> LoadSpawns(string path) =>
            File.Exists(path) ? JsonConvert.DeserializeObject<List<NpcSpawn>>(File.ReadAllText(path)) ?? new List<NpcSpawn>() : new List<NpcSpawn>();

        /// <summary>The spawns file that sits next to a map: test.rthmap -> test.spawns.json.</summary>
        public static string SpawnsPathFor(string mapPath) => Path.ChangeExtension(mapPath, null) + ".spawns.json";

        private static T ReadJson<T>(string directory, string file) where T : class
        {
            string path = Path.Combine(directory, file);
            if (!File.Exists(path)) throw new ContentException("Missing content file " + path + ".");
            try
            {
                return JsonConvert.DeserializeObject<T>(File.ReadAllText(path)) ?? throw new ContentException(file + " is empty.");
            }
            catch (JsonException e)
            {
                throw new ContentException(file + ": " + e.Message);
            }
        }

        public ContentInfoMessage ToMessage() => ContentInfoMessage.From(new ClientContent
        {
            Abilities = Abilities.Values.ToList(),
            ActionBar = ActionBar.ToList(),
            Items = Items.Values.ToList(),
            Skills = Skills.Values.ToList(),
            XpCurve = XpCurve,
            Objects = Objects.Values.ToList(),
            BileLimit = Rules.BileLimit,
            BilePassiveThreshold = Rules.BilePassiveThreshold,
            BileExtreme = Rules.BileExtreme,
            MaxPhlegm = Rules.MaxPhlegm,
        });
    }
}
