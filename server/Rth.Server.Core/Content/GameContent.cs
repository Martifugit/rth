using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Religion;
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
        /// <summary>The least a hit that lands can do; below 0 means a quarter of <see cref="MaxHit"/>, so blows aren't specks.</summary>
        public int MinHit = -1;

        public int LeastHit => MinHit >= 0 ? Math.Min(MinHit, MaxHit) : MaxHit / 4;
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
        /// <summary>An item drawn in its hand (Brother Hollis's mace), or null.</summary>
        public string? Held;
        /// <summary>Labels other systems match on ("occult-made" for Abjuration).</summary>
        public List<string> Tags = new List<string>();

        public string Examine = "";
        /// <summary>Whether players can attack it at all (townsfolk can't).</summary>
        public bool Attackable = true;
        /// <summary>Menu options besides Attack ("talk", "bank", "trade"), dispatched to whichever system registered them.</summary>
        public List<ActionDef> Actions = new List<ActionDef>();
        /// <summary>Conversation started by "talk" (dialogues.json).</summary>
        public string? Dialogue;
        /// <summary>Shop opened by "trade" (shops.json).</summary>
        public string? Shop;

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
        /// <summary>How far bile must lean at death for the choleric or melancholic lines rather than the balanced ones.</summary>
        public int LeanThreshold = 30;
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
        public int BankSize = 400;
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
        /// <summary>The character animation played for each one made (a clip or role in animations.json).</summary>
        public string Animation = "work";
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
        /// <summary>The skill that takes its share of combat XP (the Train defence setting), or null for none.</summary>
        public string? DefenceSkill;
        /// <summary>
        /// Skills that no longer exist, to the skill their XP moves into when a save is loaded
        /// (Distillation became part of Occultism).
        /// </summary>
        public Dictionary<string, string> Retired = new Dictionary<string, string>();
        public CombatLevelDef CombatLevel = new CombatLevelDef();
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
        /// <summary>Each weapon style's auto-attack; a style missing here uses <see cref="AutoAttack"/>.</summary>
        public Dictionary<CombatStyle, string> AutoAttacks = new Dictionary<CombatStyle, string>();
        public List<string> ActionBar = new List<string>();
        /// <summary>A new character's main bar for each weapon style; a style missing here uses <see cref="ActionBar"/>.</summary>
        public Dictionary<CombatStyle, List<string>> ActionBars = new Dictionary<CombatStyle, List<string>>();
        public List<NpcDef> Npcs = new List<NpcDef>();
        public AssayerLines Assayer = new AssayerLines();
        public List<ItemDef> Items = new List<ItemDef>();
        public List<DropTableDef> DropTables = new List<DropTableDef>();
        public WorldSettings World = new WorldSettings();
        public SkillsFile Skills = new SkillsFile();
        public List<ObjectDef> Objects = new List<ObjectDef>();
        public List<RecipeDef> Recipes = new List<RecipeDef>();
        public List<DialogueDef> Dialogues = new List<DialogueDef>();
        public List<ShopDef> Shops = new List<ShopDef>();
        public TasksFile Tasks = new TasksFile();
        public List<QuestDef> Quests = new List<QuestDef>();
        public List<Rth.Shared.Progress.AchievementDef> Achievements = new List<Rth.Shared.Progress.AchievementDef>();
        public List<StatusDef> Statuses = new List<StatusDef>();
        public HymnsFile Hymns = new HymnsFile();
        public List<Rth.Shared.Progress.BossDef> Bosses = new List<Rth.Shared.Progress.BossDef>();
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
            public Dictionary<CombatStyle, string> AutoAttacks = new Dictionary<CombatStyle, string>();
            public List<string> ActionBar = new List<string>();
            public Dictionary<CombatStyle, List<string>> ActionBars = new Dictionary<CombatStyle, List<string>>();
        }

        public CombatRules Rules { get; private set; } = new CombatRules();
        public IReadOnlyDictionary<string, AbilityDef> Abilities { get; private set; } = new Dictionary<string, AbilityDef>();
        /// <summary>The auto-attack with empty hands (or a weapon of no style); its style is the unarmed style.</summary>
        public AbilityDef AutoAttack { get; private set; } = new AbilityDef();
        public IReadOnlyDictionary<CombatStyle, AbilityDef> AutoAttacks { get; private set; } = new Dictionary<CombatStyle, AbilityDef>();
        /// <summary>The style of empty hands: the unarmed auto-attack's.</summary>
        public CombatStyle UnarmedStyle => AutoAttack.Style;
        public IReadOnlyList<string> ActionBar { get; private set; } = new List<string>();
        public IReadOnlyDictionary<CombatStyle, List<string>> ActionBars { get; private set; } = new Dictionary<CombatStyle, List<string>>();

        /// <summary>A new character's main bar for a weapon style.</summary>
        public IReadOnlyList<string> DefaultBarFor(CombatStyle style) => ActionBars.TryGetValue(style, out List<string>? bar) ? bar : ActionBar;
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
        public IReadOnlyDictionary<string, DialogueDef> Dialogues { get; private set; } = new Dictionary<string, DialogueDef>();
        public IReadOnlyDictionary<string, ShopDef> Shops { get; private set; } = new Dictionary<string, ShopDef>();
        public IReadOnlyDictionary<string, PointPoolDef> PointPools { get; private set; } = new Dictionary<string, PointPoolDef>();
        public IReadOnlyDictionary<string, TaskGiverDef> TaskGivers { get; private set; } = new Dictionary<string, TaskGiverDef>();
        public IReadOnlyDictionary<string, QuestDef> Quests { get; private set; } = new Dictionary<string, QuestDef>();
        public IReadOnlyDictionary<string, Rth.Shared.Progress.AchievementDef> Achievements { get; private set; } = new Dictionary<string, Rth.Shared.Progress.AchievementDef>();
        public IReadOnlyDictionary<string, StatusDef> Statuses { get; private set; } = new Dictionary<string, StatusDef>();
        public IReadOnlyDictionary<string, HymnDef> Hymns { get; private set; } = new Dictionary<string, HymnDef>();
        public IReadOnlyDictionary<string, Rth.Shared.Progress.BossDef> Bosses { get; private set; } = new Dictionary<string, Rth.Shared.Progress.BossDef>();
        /// <summary>The skill hymns need and train, and how much singing trains it.</summary>
        public HymnsFile HymnSettings { get; private set; } = new HymnsFile();
        public string? DefenceSkill { get; private set; }
        public CombatLevelDef CombatLevel { get; private set; } = new CombatLevelDef();
        public IReadOnlyDictionary<string, string> RetiredSkills { get; private set; } = new Dictionary<string, string>();

        public ItemDef? Item(string? id) => id != null && Items.TryGetValue(id, out ItemDef? def) ? def : null;

        /// <summary>The auto-attack a weapon makes: its own, else its style's, else the unarmed one.</summary>
        public AbilityDef AutoAttackFor(ItemDef? weapon)
        {
            if (weapon?.Equipment?.AutoAttack != null && Abilities.TryGetValue(weapon.Equipment.AutoAttack, out AbilityDef? own)) return own;
            CombatStyle style = CombatStyles.OfWeapon(weapon, UnarmedStyle);
            return AutoAttacks.TryGetValue(style, out AbilityDef? auto) ? auto : AutoAttack;
        }

        public string SkillName(string id) => Skills.TryGetValue(id, out SkillDef? s) ? s.Name : id;

        public static GameContent Load(string directory)
        {
            var combat = ReadJson<CombatFile>(directory, "combat.json");
            return Create(new ContentParts
            {
                Rules = combat.Rules,
                AutoAttack = combat.AutoAttack,
                AutoAttacks = combat.AutoAttacks,
                ActionBars = combat.ActionBars,
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
                Dialogues = ReadJson<List<DialogueDef>>(directory, "dialogues.json"),
                Shops = ReadJson<List<ShopDef>>(directory, "shops.json"),
                Tasks = ReadJson<TasksFile>(directory, "tasks.json"),
                Quests = ReadJson<List<QuestDef>>(directory, "quests.json"),
                Achievements = ReadJson<List<Rth.Shared.Progress.AchievementDef>>(directory, "achievements.json"),
                Statuses = ReadJson<List<StatusDef>>(directory, "statuses.json"),
                Hymns = ReadJson<HymnsFile>(directory, "hymns.json"),
                Bosses = ReadJson<List<Rth.Shared.Progress.BossDef>>(directory, "bosses.json"),
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
                if (a.PhlegmCost > a.PhlegmRequired && a.Type != AbilityType.Builder) problems.Add("Ability '" + a.Id + "': costs more phlegm than it requires.");
                if (a.OnSelf && a.DamageMax > 0) problems.Add("Ability '" + a.Id + "': keepsStall abilities and verses are used on yourself and can't deal damage.");
                if (a.BloodCostCurrent < 0 || a.BloodCostCurrent >= 100 || a.BloodCostMax < 0 || a.BloodCostMax >= 100)
                    problems.Add("Ability '" + a.Id + "': blood costs must be 0-99%.");
                if (a.BleedPercent < 0 || (a.BleedPercent > 0 && a.BleedTicks < 1)) problems.Add("Ability '" + a.Id + "': a bleed needs bleedTicks of at least 1.");
                if (a.Buff != null)
                {
                    if (a.Buff.Ticks < 1) problems.Add("Ability '" + a.Id + "': its buff needs ticks of at least 1.");
                    if (string.IsNullOrWhiteSpace(a.Buff.Status)) problems.Add("Ability '" + a.Id + "': its buff needs a status.");
                    foreach (string stat in a.Buff.Stats.Keys.Concat(a.Buff.Otherwise.Keys))
                        if (!StatIds.IsKnown(stat)) problems.Add("Ability '" + a.Id + "': its buff has unknown stat '" + stat + "'.");
                }
            }

            var autoAttacks = new Dictionary<CombatStyle, AbilityDef>();
            foreach (KeyValuePair<CombatStyle, string> pair in parts.AutoAttacks ?? new Dictionary<CombatStyle, string>())
            {
                if (!abilities.TryGetValue(pair.Value, out AbilityDef? styleAuto) || styleAuto.Type != AbilityType.Auto)
                    problems.Add("autoAttacks: the " + pair.Key + " auto-attack '" + pair.Value + "' isn't an Auto ability.");
                else autoAttacks[pair.Key] = styleAuto;
            }

            if (!abilities.TryGetValue(parts.AutoAttack ?? "", out AbilityDef? auto)) problems.Add("autoAttack '" + parts.AutoAttack + "' is not an ability.");
            else if (auto.Type != AbilityType.Auto) problems.Add("autoAttack '" + parts.AutoAttack + "' must have type Auto.");

            foreach (string id in parts.ActionBar.Concat((parts.ActionBars ?? new Dictionary<CombatStyle, List<string>>()).Values.SelectMany(b => b)))
            {
                if (!abilities.TryGetValue(id, out AbilityDef? a)) problems.Add("Action bar ability '" + id + "' doesn't exist.");
                else if (a.Type == AbilityType.Auto) problems.Add("Action bar ability '" + id + "' is an auto-attack.");
                else if (a.Type == AbilityType.Verse) problems.Add("Action bar ability '" + id + "' is a verse; the default bar holds abilities.");
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
            if (curve.MaxLevel < 2 || curve.MaxLevel > 120) problems.Add("XP curve: maxLevel must be 2 to 120.");
            foreach (DamageXpRule rule in parts.Skills.DamageXp)
                if (!skills.ContainsKey(rule.Skill)) problems.Add("Damage XP rule for " + rule.Style + ": skill '" + rule.Skill + "' doesn't exist.");
            if (parts.Skills.DefenceSkill != null && !skills.ContainsKey(parts.Skills.DefenceSkill))
                problems.Add("defenceSkill '" + parts.Skills.DefenceSkill + "' doesn't exist.");
            foreach (string s in parts.Skills.CombatLevel.Offence.Concat(parts.Skills.CombatLevel.Others.Keys))
                if (!skills.ContainsKey(s)) problems.Add("combatLevel: skill '" + s + "' doesn't exist.");
            foreach (KeyValuePair<string, string> retired in parts.Skills.Retired)
            {
                if (skills.ContainsKey(retired.Key)) problems.Add("Retired skill '" + retired.Key + "' still exists.");
                if (!skills.ContainsKey(retired.Value)) problems.Add("Retired skill '" + retired.Key + "' moves into '" + retired.Value + "', which doesn't exist.");
            }

            void CheckRequirements(IEnumerable<SkillRequirement> reqs, string where)
            {
                foreach (SkillRequirement r in reqs)
                    if (!skills.ContainsKey(r.Skill)) problems.Add(where + ": requires unknown skill '" + r.Skill + "'.");
            }
            foreach (AbilityDef a in parts.Abilities)
            {
                CheckRequirements(a.Requirements, "Ability '" + a.Id + "'");
                foreach (ItemCost r in a.Reagents)
                    if (!items.ContainsKey(r.Item) || r.Count < 1) problems.Add("Ability '" + a.Id + "': bad reagent '" + r.Item + "' x" + r.Count + ".");
                foreach (Rth.Shared.Progress.XpReward xp in a.Xp)
                    if (!skills.ContainsKey(xp.Skill)) problems.Add("Ability '" + a.Id + "': gives XP in unknown skill '" + xp.Skill + "'.");
            }
            foreach (ItemDef i in parts.Items)
            {
                if (i.Equipment == null) continue;
                CheckRequirements(i.Equipment.Requirements, "Item '" + i.Id + "'");
                string? ownAuto = i.Equipment.AutoAttack;
                if (ownAuto != null && (!abilities.TryGetValue(ownAuto, out AbilityDef? own) || own.Type != AbilityType.Auto))
                    problems.Add("Item '" + i.Id + "': autoAttack '" + ownAuto + "' isn't an Auto ability.");
                if ((i.Equipment.Style != CombatStyle.None || ownAuto != null) && i.Equipment.Slot != EquipSlot.Weapon)
                    problems.Add("Item '" + i.Id + "': only weapons have a combat style or an auto-attack.");
            }

            var toolKinds = new HashSet<string>();
            foreach (ItemDef i in parts.Items)
            {
                ToolDef? tool = i.Tool;
                if (tool == null) continue;
                string where = "Item '" + i.Id + "' (tool): ";
                if (string.IsNullOrWhiteSpace(tool.Kind)) problems.Add(where + "needs a kind.");
                else toolKinds.Add(tool.Kind);
                if (tool.Tier < 0) problems.Add(where + "tier can't be negative.");
                if (i.Stackable) problems.Add(where + "tools can't be stackable.");
                CheckRequirements(tool.Requirements, where.TrimEnd(' ', ':'));
                if (tool.Conservation < 0 || tool.Conservation > 1) problems.Add(where + "conservation must be 0-1.");
                if (tool.Extras != null && !tables.ContainsKey(tool.Extras)) problems.Add(where + "extras table '" + tool.Extras + "' doesn't exist.");
                if (tool.ReplacesExtras && tool.Extras == null) problems.Add(where + "replacesExtras needs an extras table.");
                foreach (ToolTransform t in tool.Transforms)
                {
                    if (!items.ContainsKey(t.From) || !items.ContainsKey(t.To)) problems.Add(where + "transform '" + t.From + "' to '" + t.To + "' names an unknown item.");
                    if (t.Skill != null && !skills.ContainsKey(t.Skill)) problems.Add(where + "transform skill '" + t.Skill + "' doesn't exist.");
                    if (t.Chance < 0 || t.Chance > 1) problems.Add(where + "transform chance must be 0-1.");
                }
            }

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

            Dictionary<string, DialogueDef> dialogues = Index(parts.Dialogues, d => d.Id, "dialogue", problems);
            foreach (DialogueDef d in parts.Dialogues) ValidateDialogue(d, problems);

            Dictionary<string, PointPoolDef> pools = Index(parts.Tasks.Pools, p => p.Id, "point pool", problems);
            Dictionary<string, ShopDef> shops = Index(parts.Shops, s => s.Id, "shop", problems);
            foreach (ShopDef s in parts.Shops)
            {
                string where = "Shop '" + s.Id + "': ";
                bool pointsCurrency = s.Currency.StartsWith("points:");
                if (pointsCurrency ? !pools.ContainsKey(s.Currency.Substring(7)) : !items.ContainsKey(s.Currency))
                    problems.Add(where + "currency '" + s.Currency + "' is neither an item nor a known points pool.");
                if (pointsCurrency && s.SellPercent > 0) problems.Add(where + "shops paying in points can't buy items.");
                foreach (ShopStockDef stock in s.Stock)
                {
                    if (!items.ContainsKey(stock.Item)) problems.Add(where + "item '" + stock.Item + "' doesn't exist.");
                    if (stock.Price < 1) problems.Add(where + "'" + stock.Item + "' needs a price of at least 1.");
                    if (stock.Quantity == 0 || stock.Quantity < -1) problems.Add(where + "'" + stock.Item + "' quantity must be positive or -1.");
                }
                if (s.SellPercent < 0 || s.SellPercent > 100) problems.Add(where + "sellPercent must be 0-100.");
            }

            foreach (ObjectDef o in parts.Objects)
            {
                foreach (ActionDef a in o.Actions)
                    CheckRequirements(a.Requirements, "Object '" + o.Id + "' action '" + a.Id + "'");
                NodeDef? node = o.Node;
                if (node == null) continue;
                string where = "Object '" + o.Id + "' (node): ";
                if (!skills.ContainsKey(node.Skill)) problems.Add(where + "skill '" + node.Skill + "' doesn't exist.");
                CheckRequirements(node.Requirements, where.TrimEnd(' ', ':'));
                if (node.Ticks < 1 || node.RespawnTicks < 1) problems.Add(where + "ticks and respawnTicks must be at least 1.");
                if (node.YieldsMin < 1 || node.YieldsMax < node.YieldsMin) problems.Add(where + "yieldsMin must be at least 1 and yieldsMax at least yieldsMin.");
                if (node.MinChance > node.MaxChance) problems.Add(where + "minChance is above maxChance.");
                foreach (ItemCost i in node.Items)
                    if (!items.ContainsKey(i.Item) || i.Count < 1) problems.Add(where + "bad item '" + i.Item + "' x" + i.Count + ".");
                if (node.Extras != null && !tables.ContainsKey(node.Extras)) problems.Add(where + "extras table '" + node.Extras + "' doesn't exist.");
                if (node.Points != null && !pools.ContainsKey(node.Points)) problems.Add(where + "points pool '" + node.Points + "' doesn't exist.");
                if (!o.Actions.Any(a => a.Id == "gather")) problems.Add(where + "needs a 'gather' action to be worked.");
                if (node.Tool != null && !toolKinds.Contains(node.Tool)) problems.Add(where + "no item is a '" + node.Tool + "' tool.");
                if (node.ToolTier < 0) problems.Add(where + "toolTier can't be negative.");
                if (node.DepleteChance < 0 || node.DepleteChance > 1) problems.Add(where + "depleteChance must be 0-1.");
                if (node.Surge != null && (node.Surge.EveryTicks < 1 || node.Surge.Ticks < 1)) problems.Add(where + "surge everyTicks and ticks must be at least 1.");
            }

            Dictionary<string, TaskGiverDef> givers = Index(parts.Tasks.Givers, g => g.Id, "task giver", problems);
            foreach (TaskGiverDef g in parts.Tasks.Givers)
            {
                string where = "Task giver '" + g.Id + "': ";
                if (!pools.ContainsKey(g.Pool)) problems.Add(where + "pool '" + g.Pool + "' doesn't exist.");
                CheckRequirements(g.Requirements, where.TrimEnd(' ', ':'));
                if (g.Tasks.Count == 0) problems.Add(where + "has no tasks.");
                foreach (TaskDef t in g.Tasks)
                {
                    if (t.Kind == "recipe" && !recipes.ContainsKey(t.Target)) problems.Add(where + "task '" + t.Id + "' targets unknown recipe '" + t.Target + "'.");
                    else if (t.Kind == "kill" && !parts.Npcs.Any(n => n.Id == t.Target)) problems.Add(where + "task '" + t.Id + "' targets unknown NPC '" + t.Target + "'.");
                    else if (t.Kind != "recipe" && t.Kind != "kill") problems.Add(where + "task '" + t.Id + "' has unknown kind '" + t.Kind + "'.");
                    if (t.Min < 1 || t.Max < t.Min) problems.Add(where + "task '" + t.Id + "' has a bad count range.");
                    CheckRequirements(t.Requirements, where + "task '" + t.Id + "'");
                }
            }

            Dictionary<string, QuestDef> quests = Index(parts.Quests, q => q.Id, "quest", problems);
            foreach (QuestDef q in parts.Quests)
            {
                string where = "Quest '" + q.Id + "': ";
                if (q.Stages.Count == 0) problems.Add(where + "has no stages.");
                var numbers = new HashSet<int>();
                foreach (QuestStage s in q.Stages)
                {
                    if (s.Stage < 1 || !numbers.Add(s.Stage)) problems.Add(where + "stage numbers must be unique and at least 1.");
                    if (s.Next != null && !q.Stages.Any(o => o.Stage == s.Next)) problems.Add(where + "stage " + s.Stage + " moves to missing stage " + s.Next + ".");
                    if (s.Objective != null)
                    {
                        QuestObjective o = s.Objective;
                        bool ok = o.Kind == "kill" ? parts.Npcs.Any(n => n.Id == o.Target)
                            : o.Kind == "recipe" ? recipes.ContainsKey(o.Target)
                            : o.Kind == "item" && items.ContainsKey(o.Target);
                        if (!ok) problems.Add(where + "stage " + s.Stage + " has a bad objective (" + o.Kind + " " + o.Target + ").");
                        if (s.Next == null) problems.Add(where + "stage " + s.Stage + " has an objective but no next stage.");
                    }
                }
                CheckRequirements(q.Requirements, where.TrimEnd(' ', ':'));
                foreach (SkillXp xp in q.Rewards.Xp)
                    if (!skills.ContainsKey(xp.Skill)) problems.Add(where + "rewards XP in unknown skill '" + xp.Skill + "'.");
                foreach (ItemCost item in q.Rewards.Items)
                    if (!items.ContainsKey(item.Item)) problems.Add(where + "rewards unknown item '" + item.Item + "'.");
            }

            Dictionary<string, Rth.Shared.Progress.AchievementDef> achievements = Index(parts.Achievements, a => a.Id, "achievement", problems);
            foreach (Rth.Shared.Progress.AchievementDef a in parts.Achievements)
            {
                string where = "Achievement '" + a.Id + "': ";
                if (string.IsNullOrWhiteSpace(a.Name)) problems.Add(where + "has no name.");
                Rth.Shared.Progress.Objective o = a.Objective;
                bool ok = o.Kind switch
                {
                    "kill" => parts.Npcs.Any(n => n.Id == o.Target),
                    "talk" => parts.Npcs.Any(n => n.Id == o.Target),
                    "recipe" => recipes.ContainsKey(o.Target),
                    "level" => skills.ContainsKey(o.Target),
                    "quest" => quests.ContainsKey(o.Target),
                    "obtain" => items.ContainsKey(o.Target),
                    "object" => o.Target.Contains(":") && objects.ContainsKey(o.Target.Split(':')[0]),
                    "region" => o.Target.Length > 0,
                    _ => false,
                };
                if (!ok) problems.Add(where + "has a bad objective (" + o.Kind + " " + o.Target + ").");
                if (o.Count < 1) problems.Add(where + "objective count must be at least 1.");
                foreach (Rth.Shared.Progress.XpReward xp in a.Reward.Xp)
                    if (!skills.ContainsKey(xp.Skill)) problems.Add(where + "rewards XP in unknown skill '" + xp.Skill + "'.");
                foreach (ItemCost item in a.Reward.Items)
                    if (!items.ContainsKey(item.Item)) problems.Add(where + "rewards unknown item '" + item.Item + "'.");
            }

            foreach (NpcDef n in parts.Npcs)
            {
                if (n.Dialogue != null && !dialogues.ContainsKey(n.Dialogue)) problems.Add("NPC '" + n.Id + "': dialogue '" + n.Dialogue + "' doesn't exist.");
                if (n.Shop != null && !shops.ContainsKey(n.Shop)) problems.Add("NPC '" + n.Id + "': shop '" + n.Shop + "' doesn't exist.");
                if (n.Actions.Any(a => a.Id == "talk") && n.Dialogue == null) problems.Add("NPC '" + n.Id + "': has Talk-to but no dialogue.");
                if (n.Actions.Any(a => a.Id == "trade") && n.Shop == null) problems.Add("NPC '" + n.Id + "': has Trade but no shop.");
            }

            CombatRules rules = parts.Rules;
            if (rules.BileLimit <= 0) problems.Add("Rules: bileLimit must be positive.");

            Dictionary<string, StatusDef> statuses = Index(parts.Statuses, st => st.Id, "status", problems);
            foreach (StatusDef st in parts.Statuses)
                if (string.IsNullOrWhiteSpace(st.Name)) problems.Add("Status '" + st.Id + "': needs a name.");
            IEnumerable<string> givenOut = StatusIds.BuiltIn.Concat(Enum.GetValues(typeof(EffectType)).Cast<EffectType>()
                .Where(t => t != EffectType.None).Select(StatusIds.ForEffect));
            foreach (string id in givenOut)
                if (!statuses.ContainsKey(id)) problems.Add("Statuses: '" + id + "' is given out by the server but not defined.");
            foreach (AbilityDef a in parts.Abilities)
                if (a.Buff != null && !string.IsNullOrWhiteSpace(a.Buff.Status) && !statuses.ContainsKey(a.Buff.Status))
                    problems.Add("Ability '" + a.Id + "': buff status '" + a.Buff.Status + "' isn't in statuses.json.");

            HymnsFile hymnFile = parts.Hymns ?? new HymnsFile();
            Dictionary<string, HymnDef> hymns = Index(hymnFile.Hymns, h => h.Id, "hymn", problems);
            if (!skills.ContainsKey(hymnFile.Skill)) problems.Add("Hymns: skill '" + hymnFile.Skill + "' doesn't exist.");
            foreach (HymnDef h in hymnFile.Hymns)
            {
                string where = "Hymn '" + h.Id + "'";
                if (string.IsNullOrWhiteSpace(h.Name)) problems.Add(where + ": needs a name.");
                if (h.Drain < 0) problems.Add(where + ": drain can't be negative.");
                foreach (string stat in h.Stats.Keys)
                    if (!StatIds.IsKnown(stat)) problems.Add(where + ": unknown stat '" + stat + "'.");
                CheckRequirements(h.Requirements, where);
            }

            Dictionary<string, Rth.Shared.Progress.BossDef> bosses = Index(parts.Bosses ?? new List<Rth.Shared.Progress.BossDef>(), b => b.Id, "boss", problems);
            foreach (Rth.Shared.Progress.BossDef b in bosses.Values)
            {
                if (string.IsNullOrWhiteSpace(b.Name)) problems.Add("Boss '" + b.Id + "': needs a name.");
                if (b.Rewards != null && !tables.ContainsKey(b.Rewards)) problems.Add("Boss '" + b.Id + "': rewards table '" + b.Rewards + "' doesn't exist.");
            }

            if (problems.Count > 0) throw new ContentException(string.Join(Environment.NewLine, problems));

            AddBankNotes(items);

            return new GameContent
            {
                Rules = rules,
                Abilities = abilities,
                AutoAttack = auto!,
                AutoAttacks = autoAttacks,
                ActionBar = parts.ActionBar.ToList(),
                ActionBars = parts.ActionBars ?? new Dictionary<CombatStyle, List<string>>(),
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
                Dialogues = dialogues,
                Shops = shops,
                PointPools = pools,
                TaskGivers = givers,
                Quests = quests,
                Achievements = achievements,
                Statuses = statuses,
                Hymns = hymns,
                HymnSettings = hymnFile,
                Bosses = bosses,
                DefenceSkill = parts.Skills.DefenceSkill,
                RetiredSkills = parts.Skills.Retired,
                CombatLevel = parts.Skills.CombatLevel,
            };
        }

        /// <summary>
        /// Every item that doesn't stack gets a bank note: a stackable paper standing for it, which
        /// the bank hands out on request and takes back as the real thing.
        /// </summary>
        private static void AddBankNotes(Dictionary<string, ItemDef> items)
        {
            foreach (ItemDef item in items.Values.ToList())
            {
                if (item.Stackable || item.NoteOf != null) continue;
                string id = item.Id + ".note";
                if (items.ContainsKey(id)) continue;
                item.Note = id;
                items[id] = new ItemDef
                {
                    Id = id,
                    Name = item.Name,
                    Examine = "A Synod bank note for " + item.Name.ToLowerInvariant() + ". Any strongbox will exchange it.",
                    Stackable = true,
                    Value = item.Value,
                    Color = item.Color,
                    Icon = item.Icon,
                    NoteOf = item.Id,
                    Tags = new List<string> { "note" },
                };
            }
        }

        private static readonly HashSet<string> KnownEffectOps = new HashSet<string>
        {
            EffectOp.HealBlood, EffectOp.HealBloodPercent, EffectOp.RestoreDevotion, EffectOp.RestoreDevotionPercent, EffectOp.AddPhlegm,
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

        /// <summary>Structure only: every jump lands on a node, every condition and action has a type.
        /// Whether a type has a handler is checked at runtime (and by tests), since systems register them.</summary>
        private static void ValidateDialogue(DialogueDef d, List<string> problems)
        {
            string where = "Dialogue '" + d.Id + "': ";
            if (!d.Nodes.ContainsKey(d.Start)) problems.Add(where + "start node '" + d.Start + "' doesn't exist.");
            foreach (KeyValuePair<string, DialogueNode> pair in d.Nodes)
            {
                DialogueNode node = pair.Value;
                string at = where + "node '" + pair.Key + "': ";
                if (node.Next != null && !d.Nodes.ContainsKey(node.Next)) problems.Add(at + "next '" + node.Next + "' doesn't exist.");
                foreach (DialogueBranch b in node.Branches)
                {
                    if (!d.Nodes.ContainsKey(b.Next)) problems.Add(at + "branch to '" + b.Next + "' doesn't exist.");
                    if (b.Conditions.Any(c => string.IsNullOrEmpty(c.Type))) problems.Add(at + "a branch condition has no type.");
                }
                foreach (DialogueOption o in node.Options)
                {
                    if (o.Next != null && !d.Nodes.ContainsKey(o.Next)) problems.Add(at + "option '" + o.Text + "' goes to '" + o.Next + "', which doesn't exist.");
                    if (o.Conditions.Any(c => string.IsNullOrEmpty(c.Type)) || o.Actions.Any(a => string.IsNullOrEmpty(a.Type)))
                        problems.Add(at + "option '" + o.Text + "' has a condition or action without a type.");
                }
                if (node.Actions.Any(a => string.IsNullOrEmpty(a.Type))) problems.Add(at + "an action has no type.");
                if (node.Speaker != "npc" && node.Speaker != "player") problems.Add(at + "speaker must be npc or player.");
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
            ActionBars = ActionBars.ToDictionary(p => p.Key, p => p.Value.ToList()),
            AutoAttack = AutoAttack.Id,
            Items = Items.Values.ToList(),
            Skills = Skills.Values.ToList(),
            XpCurve = XpCurve,
            Objects = Objects.Values.ToList(),
            Npcs = Npcs.Values.Select(n => new NpcInfo
            {
                Id = n.Id, Name = n.Name, Examine = n.Examine, Attackable = n.Attackable, Actions = n.Actions,
                Blood = n.Blood, MaxHit = n.MaxHit, Style = n.Style, Aggressive = n.Aggressive,
            }).ToList(),
            Recipes = Recipes.Values.Select(r => new RecipeInfo
            {
                Id = r.Id, Name = r.Name, Verb = r.Verb, Skill = r.Skill, Level = r.Level, Station = r.Station, Xp = r.Xp,
                Inputs = r.Inputs.Select(i => new ItemCost { Item = i.Item, Count = i.Count }).ToList(),
            }).ToList(),
            Achievements = Achievements.Values.ToList(),
            Statuses = Statuses.Values.ToList(),
            Hymns = Hymns.Values.ToList(),
            Bosses = Bosses.Values.ToList(),
            CombatLevel = CombatLevel,
            BileLimit = Rules.BileLimit,
            MaxPhlegm = Rules.MaxPhlegm,
            GlobalCooldownTicks = Rules.GlobalCooldownTicks,
            ScarsForFreeUltimate = Rules.ScarsForFreeUltimate,
        });
    }
}
