using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Rth.Shared.Combat;
using Rth.Shared.Net;

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

    public sealed class ContentException : Exception
    {
        public ContentException(string message) : base(message) { }
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

        public static GameContent Load(string directory)
        {
            var combat = ReadJson<CombatFile>(directory, "combat.json");
            var abilities = ReadJson<List<AbilityDef>>(directory, "abilities.json");
            var npcs = ReadJson<List<NpcDef>>(directory, "npcs.json");
            var assayer = ReadJson<AssayerLines>(directory, "assayer.json");
            return Create(combat.Rules, abilities, combat.AutoAttack, combat.ActionBar, npcs, assayer);
        }

        /// <summary>Builds and validates content from parts; throws <see cref="ContentException"/> listing every problem.</summary>
        public static GameContent Create(CombatRules rules, IEnumerable<AbilityDef> abilities, string autoAttack,
            IEnumerable<string> actionBar, IEnumerable<NpcDef> npcs, AssayerLines? assayer = null)
        {
            var problems = new List<string>();
            var abilityMap = new Dictionary<string, AbilityDef>();
            foreach (AbilityDef a in abilities)
            {
                if (string.IsNullOrWhiteSpace(a.Id)) problems.Add("An ability has no id.");
                else if (abilityMap.ContainsKey(a.Id)) problems.Add("Duplicate ability id '" + a.Id + "'.");
                else abilityMap.Add(a.Id, a);
                if (a.DamageMin < 0 || a.DamageMax < a.DamageMin) problems.Add("Ability '" + a.Id + "': damage range is invalid.");
                if (a.Range < 1) problems.Add("Ability '" + a.Id + "': range must be at least 1.");
                if (a.CooldownTicks < 0) problems.Add("Ability '" + a.Id + "': negative cooldown.");
                if (a.BileMin > a.BileMax) problems.Add("Ability '" + a.Id + "': bileMin is above bileMax.");
                if (a.PhlegmCost > a.PhlegmRequired && a.Type != AbilityType.Basic) problems.Add("Ability '" + a.Id + "': costs more phlegm than it requires.");
            }

            if (!abilityMap.TryGetValue(autoAttack ?? "", out AbilityDef? auto)) problems.Add("autoAttack '" + autoAttack + "' is not an ability.");
            else if (auto.Type != AbilityType.Auto) problems.Add("autoAttack '" + autoAttack + "' must have type Auto.");

            var bar = actionBar.ToList();
            foreach (string id in bar)
            {
                if (!abilityMap.TryGetValue(id, out AbilityDef? a)) problems.Add("Action bar ability '" + id + "' doesn't exist.");
                else if (a.Type == AbilityType.Auto) problems.Add("Action bar ability '" + id + "' is an auto-attack.");
            }

            var npcMap = new Dictionary<string, NpcDef>();
            foreach (NpcDef n in npcs)
            {
                if (string.IsNullOrWhiteSpace(n.Id)) problems.Add("An NPC has no id.");
                else if (npcMap.ContainsKey(n.Id)) problems.Add("Duplicate NPC id '" + n.Id + "'.");
                else npcMap.Add(n.Id, n);
                if (n.Blood <= 0) problems.Add("NPC '" + n.Id + "': blood must be positive.");
                if (n.AttackRange < 1 || n.AttackTicks < 1) problems.Add("NPC '" + n.Id + "': attackRange and attackTicks must be at least 1.");
                try
                {
                    _ = n.ColorRgb;
                }
                catch (FormatException)
                {
                    problems.Add("NPC '" + n.Id + "': color '" + n.Color + "' isn't #RRGGBB.");
                }
            }

            if (rules.BilePassiveThreshold <= 0 || rules.BileExtreme <= rules.BilePassiveThreshold || rules.BileLimit < rules.BileExtreme)
                problems.Add("Rules: need 0 < bilePassiveThreshold < bileExtreme <= bileLimit.");

            if (problems.Count > 0) throw new ContentException(string.Join(Environment.NewLine, problems));

            return new GameContent
            {
                Rules = rules,
                Abilities = abilityMap,
                AutoAttack = auto!,
                ActionBar = bar,
                Npcs = npcMap,
                Assayer = assayer ?? new AssayerLines(),
            };
        }

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

        public ContentInfoMessage ToMessage() => new ContentInfoMessage
        {
            Abilities = Abilities.Values.ToList(),
            ActionBar = ActionBar.ToList(),
            BileLimit = Rules.BileLimit,
            BilePassiveThreshold = Rules.BilePassiveThreshold,
            BileExtreme = Rules.BileExtreme,
            MaxPhlegm = Rules.MaxPhlegm,
        };
    }
}
