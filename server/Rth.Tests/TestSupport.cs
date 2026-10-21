using System.Collections.Concurrent;
using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Skills;
using Rth.Server.Net;
using Rth.Server.Persistence;
using Rth.Server.Timing;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>An in-memory connection: tests push client messages in and read what the server sent.</summary>
internal sealed class FakeConnection : IConnection
{
    private readonly ConcurrentQueue<Message> _inbox = new();

    public List<Message> Sent { get; } = new();
    public bool IsOpen { get; private set; } = true;
    public string Description => "fake";

    public void FromClient(Message message) => _inbox.Enqueue(message);
    public bool TryReceive(out Message message) => _inbox.TryDequeue(out message!);

    public void Send(Message message)
    {
        if (IsOpen) Sent.Add(message);
    }

    public void Close() => IsOpen = false;

    /// <summary>Simulates the client vanishing without a logout message.</summary>
    public void Drop() => IsOpen = false;

    public IEnumerable<WorldUpdateMessage> Updates => Sent.OfType<WorldUpdateMessage>();
    public IEnumerable<string> Texts => Sent.OfType<GameTextMessage>().Select(t => t.Text);
}

internal sealed class MemoryPlayerStore : IPlayerStore
{
    public Dictionary<string, PlayerSave> Saves { get; } = new();
    public int SaveCount { get; private set; }

    public PlayerSave? Load(string key) => Saves.TryGetValue(key, out PlayerSave? save) ? save : null;

    public void Save(string key, PlayerSave save)
    {
        SaveCount++;
        Saves[key] = save;
    }
}

/// <summary>Simulated time: waiting advances the clock instantly.</summary>
internal sealed class FakeClock : IClock
{
    public long Now { get; set; }

    public bool Wait(long duration, WaitHandle cancel)
    {
        Now += Math.Max(0, duration);
        return cancel.WaitOne(0);
    }
}

internal static class Maps
{
    /// <summary>
    /// Builds a map from rows of text, top row = north. '.' is open, '#' is blocked, 'S' is the
    /// spawn (open). The bottom-left character is tile (0, 0).
    /// </summary>
    public static MapData FromText(params string[] rows)
    {
        int height = rows.Length;
        int width = rows[0].Length;
        var map = new MapData(0, 0, width, height);
        for (int row = 0; row < height; row++)
        {
            int y = height - 1 - row;
            for (int x = 0; x < width; x++)
            {
                char c = rows[row][x];
                if (c == '#') map.Block(0, x, y);
                if (c == 'S') map.Spawn = new TileCoord(x, y);
            }
        }
        return map;
    }

    public static MapData Open(int width, int height, TileCoord spawn)
    {
        return new MapData(0, 0, width, height) { Spawn = spawn };
    }
}

/// <summary>
/// Small, fixed content for tests, so tuning the real data files never breaks them. Every attack
/// lands (accuracy 1, no evasion) and does exactly its listed damage.
/// </summary>
internal static class TestContent
{
    public static CombatRules Rules() => new CombatRules { PlayerAccuracy = 1.0, PlayerAbilityDamage = 100 };

    public static List<AbilityDef> Abilities() => new()
    {
        new AbilityDef { Id = "zap", Name = "Zap", Type = AbilityType.Auto, CooldownTicks = 4, Range = 8, DamageMin = 50, DamageMax = 50 },
        new AbilityDef { Id = "spark", Name = "Spark", Type = AbilityType.Builder, CooldownTicks = 5, Range = 8, DamageMin = 100, DamageMax = 100, PhlegmGain = 10, BileShift = 10 },
        new AbilityDef { Id = "draught", Name = "Draught", Type = AbilityType.Builder, CooldownTicks = 5, Range = 8, DamageMin = 100, DamageMax = 100, PhlegmGain = 10, BileShift = -10, HealPercentOfDamage = 50 },
        new AbilityDef { Id = "still", Name = "Still", Type = AbilityType.Builder, CooldownTicks = 5, Range = 8, DamageMin = 10, DamageMax = 10, PhlegmGain = 5, BileTowardZero = 25 },
        new AbilityDef { Id = "flare", Name = "Flare", Type = AbilityType.Spender, CooldownTicks = 10, Range = 8, DamageMin = 200, DamageMax = 200, PhlegmRequired = 50, PhlegmCost = 15, BileShift = 15, BileMin = 30 },
        new AbilityDef { Id = "shroud", Name = "Shroud", Type = AbilityType.Spender, CooldownTicks = 10, Range = 8, DamageMin = 100, DamageMax = 100, PhlegmRequired = 50, PhlegmCost = 15, BileShift = -15, BileMax = -30, Effect = EffectType.Ward, EffectMagnitude = 30, EffectTicks = 10 },
        new AbilityDef { Id = "ult", Name = "Ult", Type = AbilityType.Ultimate, CooldownTicks = 30, Range = 8, DamageMin = 400, DamageMax = 400, PhlegmRequired = 100, PhlegmCost = 100 },
        new AbilityDef { Id = "poke", Name = "Poke", Type = AbilityType.Builder, CooldownTicks = 3, Range = 1, DamageMin = 10, DamageMax = 10, PhlegmGain = 1 },
        new AbilityDef { Id = "secret", Name = "Secret", Type = AbilityType.Builder, CooldownTicks = 3, Range = 8, DamageMin = 10, DamageMax = 10, Style = CombatStyle.Occult, Requirements = { new SkillRequirement { Skill = "occultism", Level = 3 } } },
    };

    public static List<NpcDef> Npcs() => new()
    {
        new NpcDef { Id = "dummy", Name = "Dummy", Blood = 100000, Attacks = false, Regenerates = true, WanderRadius = 0, Evasion = 0, Shape = EntityShape.Cylinder },
        new NpcDef { Id = "rat", Name = "Rat", Blood = 300, Style = CombatStyle.Melee, AttackRange = 1, AttackTicks = 4, MaxHit = 10, Accuracy = 1.0, Evasion = 0, WanderRadius = 0, LeashRadius = 10, RespawnTicks = 5, DropTable = "rat" },
        new NpcDef { Id = "brute", Name = "Brute", Blood = 5000, Style = CombatStyle.Melee, AttackRange = 1, AttackTicks = 2, MaxHit = 2000, Accuracy = 1.0, Evasion = 0, WanderRadius = 0 },
        new NpcDef { Id = "mite", Name = "Mite", Blood = 300, Style = CombatStyle.Occult, AttackRange = 5, AttackTicks = 5, MaxHit = 50, Accuracy = 1.0, Evasion = 0, Aggressive = true, AggroRadius = 4, WanderRadius = 0 },
    };

    public static AssayerLines Assayer() => new()
    {
        First = { "first {name}" },
        Choleric = { "hot" },
        Melancholic = { "cold" },
        Balanced = { "level" },
        Frequent = { "again {deaths}" },
    };

    public static List<ItemDef> Items() => new()
    {
        new ItemDef { Id = "coins", Name = "Coins", Stackable = true },
        new ItemDef { Id = "bread", Name = "Bread", Examine = "Bready.", Consumable = new ConsumableDef { Verb = "Eat", CooldownTicks = 2, AttackDelayTicks = 2, Effects = { new EffectOp { Op = EffectOp.HealBlood, Amount = 100 } } } },
        new ItemDef { Id = "tonic", Name = "Tonic", Consumable = new ConsumableDef { Verb = "Drink", CooldownGroup = "potion", CooldownTicks = 10, AttackDelayTicks = 1, Leaves = "vial", Effects = { new EffectOp { Op = EffectOp.AddPhlegm, Amount = 30 }, new EffectOp { Op = EffectOp.ShiftBile, Amount = -20 } } } },
        new ItemDef { Id = "vial", Name = "Vial" },
        new ItemDef { Id = "sword", Name = "Sword", Equipment = new EquipmentDef { Slot = EquipSlot.Weapon, Stats = { [StatIds.AbilityDamage] = 50 } } },
        new ItemDef { Id = "club", Name = "Club", Equipment = new EquipmentDef { Slot = EquipSlot.Weapon, Stats = { [StatIds.AbilityDamage] = 20 } } },
        new ItemDef { Id = "hat", Name = "Hat", Equipment = new EquipmentDef { Slot = EquipSlot.Head, Stats = { [StatIds.Ward] = 10, [StatIds.MaxBlood] = 100 } } },
        new ItemDef { Id = "gem", Name = "Gem" },
        new ItemDef { Id = "carcass", Name = "Carcass" },
        new ItemDef { Id = "big_carcass", Name = "Big carcass" },
        new ItemDef { Id = "knife", Name = "Knife" },
        new ItemDef { Id = "crown", Name = "Crown", Equipment = new EquipmentDef { Slot = EquipSlot.Head, Requirements = { new SkillRequirement { Skill = "occultism", Level = 3 } } } },
    };

    public static List<DropTableDef> DropTables() => new()
    {
        new DropTableDef { Id = "rat", Always = { new DropEntry { Item = "coins", Min = 5, Max = 5 } } },
        new DropTableDef { Id = "gems", Rolls = 1, Entries = { new DropEntry { Item = "gem" } } },
        new DropTableDef { Id = "nested", Always = { new DropEntry { Table = "gems" } }, Rolls = 3, Entries = { new DropEntry { Item = "coins", Min = 1, Max = 3 } }, NothingWeight = 1 },
    };

    public static SkillsFile Skills() => new()
    {
        XpCurve = new XpCurve { MaxLevel = 100 },
        Skills =
        {
            new SkillDef { Id = "occultism", Name = "Occultism" },
            new SkillDef { Id = "butchering", Name = "Butchering" },
            new SkillDef { Id = "might", Name = "Might", StatsPerLevel = { [StatIds.AbilityDamage] = 3 } },
        },
        DamageXp = { new DamageXpRule { Style = CombatStyle.Occult, Skill = "occultism", XpPerDamage = 0.5 } },
    };

    public static List<ObjectDef> Objects() => new()
    {
        new ObjectDef { Id = "rack", Name = "Rack", Examine = "A rack.", Actions = { new ActionDef { Id = "process", Label = "Butcher" } } },
        new ObjectDef { Id = "vat", Name = "Vat", Actions = { new ActionDef { Id = "process", Label = "Butcher" } } },
        new ObjectDef { Id = "sign", Name = "Signpost", Actions = { new ActionDef { Id = "read", Label = "Read", Message = "Tolling, west." } } },
        new ObjectDef { Id = "gate", Name = "Gate", Actions = { new ActionDef { Id = "open", Label = "Open", RequiresFlag = "gate_open", LockedMessage = "Barred.", Message = "It creaks open." } } },
    };

    public static List<RecipeDef> Recipes() => new()
    {
        new RecipeDef
        {
            Id = "rat", Name = "rat carcass", Verb = "butcher", Skill = "butchering", Level = 1, Station = "rack",
            Inputs = { new ItemAmount { Item = "carcass" } }, Tools = { "knife" }, Outputs = { new ItemAmount { Item = "coins", Count = 2 } },
            OutputTable = "gems", ExtraRollEveryLevels = 5, Ticks = 3, Xp = 50,
        },
        new RecipeDef
        {
            Id = "big", Name = "big carcass", Verb = "butcher", Skill = "butchering", Level = 5, Station = "vat",
            Inputs = { new ItemAmount { Item = "big_carcass" } }, Outputs = { new ItemAmount { Item = "gem", Count = 2 } }, Ticks = 2, Xp = 10,
        },
    };

    public static List<NpcDef> TownNpcs() => new()
    {
        new NpcDef { Id = "banker", Name = "Banker", Attackable = false, Attacks = false, WanderRadius = 0, Actions = { new ActionDef { Id = "bank", Label = "Bank" } } },
        new NpcDef { Id = "merchant", Name = "Merchant", Attackable = false, Attacks = false, WanderRadius = 0, Shop = "general", Dialogue = "merchant",
            Actions = { new ActionDef { Id = "talk", Label = "Talk-to" }, new ActionDef { Id = "trade", Label = "Trade" }, new ActionDef { Id = "secret", Label = "Whisper", RequiresFlag = "trusted", LockedMessage = "Not yet." } } },
    };

    /// <summary>A small conversation exercising branches, conditions, actions and tokens.</summary>
    public static DialogueDef MerchantDialogue() => new()
    {
        Id = "merchant",
        Nodes =
        {
            ["start"] = new DialogueNode
            {
                Branches = { new DialogueBranch { Conditions = { new DialogueCondition { Type = "flag", Key = "met", Not = true } }, Next = "intro" } },
                Next = "greet",
            },
            ["intro"] = new DialogueNode { Text = "Welcome, {player}.", Actions = { new DialogueAction { Type = "setFlag", Key = "met" } }, Next = "greet" },
            ["greet"] = new DialogueNode
            {
                Text = "What'll it be? You have {points:tokens} tokens.",
                Options =
                {
                    new DialogueOption { Text = "Trade", Actions = { new DialogueAction { Type = "openShop", Id = "general" } } },
                    new DialogueOption { Text = "Work", Next = "work" },
                    new DialogueOption { Text = "Rich option", Conditions = { new DialogueCondition { Type = "hasItem", Item = "coins", Count = 100 } }, Next = "rich" },
                    new DialogueOption { Text = "Bank", Actions = { new DialogueAction { Type = "openBank" } } },
                    new DialogueOption { Text = "Bye" },
                },
            },
            ["rich"] = new DialogueNode { Text = "Ooh.", Actions = { new DialogueAction { Type = "giveItem", Item = "gem" } } },
            ["work"] = new DialogueNode
            {
                Branches =
                {
                    new DialogueBranch { Conditions = { new DialogueCondition { Type = "hasTask" } }, Next = "busy" },
                    new DialogueBranch { Conditions = { new DialogueCondition { Type = "canTakeTask", Id = "boss" } }, Next = "assign" },
                },
                Text = "{whyNoTask:boss}",
            },
            ["assign"] = new DialogueNode { Text = "Go: {task}", Actions = { new DialogueAction { Type = "assignTask", Id = "boss" } } },
            ["busy"] = new DialogueNode { Text = "Busy with {task} for {taskGiver}." },
        },
    };

    public static List<ShopDef> Shops() => new()
    {
        new ShopDef
        {
            Id = "general", Name = "General", Currency = "coins", SellPercent = 50,
            Stock = { new ShopStockDef { Item = "bread", Price = 5, Quantity = 2, RestockTicks = 10 }, new ShopStockDef { Item = "gem", Price = 50 } },
        },
        new ShopDef { Id = "token_shop", Name = "Tokens", Currency = "points:tokens", Stock = { new ShopStockDef { Item = "sword", Price = 10 } } },
    };

    public static TasksFile Tasks() => new()
    {
        Pools = { new PointPoolDef { Id = "tokens", Name = "Tokens" } },
        Givers =
        {
            new TaskGiverDef
            {
                Id = "boss", Name = "Boss", Pool = "tokens", StreakEvery = 3, StreakMultiplier = 2, CancelCost = 2,
                Tasks = { new TaskDef { Id = "rats", Kind = "kill", Target = "rat", Min = 2, Max = 2, Points = 5, Description = "Kill {count} rats." } },
            },
            new TaskGiverDef
            {
                Id = "elite", Name = "Elite", Pool = "tokens", Requirements = { new SkillRequirement { Skill = "butchering", Level = 5 } },
                Tasks = { new TaskDef { Id = "butcher", Kind = "recipe", Target = "rat", Min = 1, Max = 1, Points = 3, Description = "Butcher {count}." } },
            },
        },
    };

    public static ContentParts Parts(CombatRules? rules = null, List<AbilityDef>? abilities = null) => new()
    {
        Dialogues = { MerchantDialogue() },
        Shops = Shops(),
        Tasks = Tasks(),
        Skills = Skills(),
        Objects = Objects(),
        Recipes = Recipes(),
        Rules = rules ?? Rules(),
        Abilities = abilities ?? Abilities(),
        AutoAttack = "zap",
        ActionBar = new List<string> { "spark", "draught", "still", "flare", "shroud", "ult", "poke", "secret" },
        Npcs = Npcs().Concat(TownNpcs()).ToList(),
        Assayer = Assayer(),
        Items = Items(),
        DropTables = DropTables(),
        World = new WorldSettings { InventorySize = 28, GroundItemPrivateTicks = 10, GroundItemDespawnTicks = 30 },
    };

    public static GameContent Create(CombatRules? rules = null, List<AbilityDef>? abilities = null) =>
        GameContent.Create(Parts(rules, abilities));
}

/// <summary>A world with a fake store and helpers for logging in fake clients.</summary>
internal sealed class WorldFixture
{
    public WorldFixture(CollisionMap map, ServerConfig? config = null, GameContent? content = null, IEnumerable<NpcSpawn>? spawns = null, IEnumerable<PlacedObject>? objects = null)
    {
        Config = config ?? new ServerConfig();
        Config.RandomSeed ??= 12345;
        Store = new MemoryPlayerStore();
        World = new World(Config, map, Store, content ?? TestContent.Create(),
            new MapPlacements { Npcs = (spawns ?? Enumerable.Empty<NpcSpawn>()).ToList(), Objects = (objects ?? Enumerable.Empty<PlacedObject>()).ToList() });
    }

    public Npc Npc(string defId) => World.Npcs.First(n => n.Def.Id == defId);

    public ServerConfig Config { get; }
    public MemoryPlayerStore Store { get; }
    public World World { get; }

    public FakeConnection Login(string name)
    {
        var connection = new FakeConnection();
        World.AcceptConnection(connection);
        connection.FromClient(new LoginMessage { Name = name });
        World.Tick();
        return connection;
    }

    public void Ticks(int count)
    {
        for (int i = 0; i < count; i++) World.Tick();
    }

    public Player Player(string name) => World.FindPlayer(name) ?? throw new InvalidOperationException(name + " is not logged in.");
}
