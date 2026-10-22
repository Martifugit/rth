using Rth.Server.Content;
using Rth.Server.Persistence;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

public class ContentAndRulesTests
{
    [Fact]
    public void TheShippedContentLoadsAndValidates()
    {
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        Assert.Equal(8, content.ActionBar.Count);
        Assert.Equal(AbilityType.Auto, content.AutoAttack.Type);
        Assert.Contains("fen_rat", content.Npcs.Keys);
        Assert.NotEmpty(content.Death.Lines.First);
    }

    [Fact]
    public void TheShippedRegionsFitTogetherIntoOneWorld()
    {
        WorldLoader.Result world = WorldLoader.Load(Path.Combine(Repo.ServerDirectory, "data", "maps"), "tolling");
        Assert.Contains(world.Map.Regions, r => r.Name == "tolling" && r.X == 0 && r.Y == 0);
        Assert.Equal(new TileCoord(33, 53), world.Map.Spawn);
        Assert.False(world.Map.IsBlocked(0, world.Map.Spawn.X, world.Map.Spawn.Y));

        // The dead wake on open ground (death.json), not silently at the spawn because the tile is gone.
        TilePlace? wake = GameContent.Load(Repo.ContentDirectory).Death.Wake;
        if (wake != null)
            Assert.True(world.Map.Contains(wake.Plane, wake.X, wake.Y) && !world.Map.IsBlocked(wake.Plane, wake.X, wake.Y), "death.json's wake tile isn't open ground");

        // The dead wake on the landing, and can walk to the ferryman and to a bank there.
        if (wake != null && world.Map.FindRegion("realm_of_the_dead") is WorldRegion realm)
        {
            GameContent content = GameContent.Load(Repo.ContentDirectory);
            Assert.True(realm.Contains(wake.X, wake.Y), "death.json's wake tile isn't on the landing");
            var start = new TileCoord(wake.X, wake.Y);
            NpcSpawn ferryman = Assert.Single(world.Placements.Npcs, n => n.Npc == "ferryman" && realm.Contains(n.X, n.Y));
            Assert.Equal(new TileCoord(ferryman.X, ferryman.Y), Pathfinder.FindPath(world.Map, wake.Plane, start, new TileCoord(ferryman.X, ferryman.Y))[^1]);
            PlacedObject bank = Assert.Single(world.Placements.Objects, o => realm.Contains(o.X, o.Y) &&
                content.Objects.TryGetValue(o.Type, out var def) && def.Actions.Any(a => a.Id == "bank"));
            var beside = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }
                .Select(d => new TileCoord(bank.X + d.Item1, bank.Y + d.Item2))
                .Where(t => !world.Map.IsBlocked(bank.Plane, t.X, t.Y));
            Assert.Contains(beside, t => Pathfinder.FindPath(world.Map, wake.Plane, start, t) is { Count: > 0 } p && p[^1] == t);
        }

        // Palmerway joins Tolling: walk from the town's east road over the border.
        if (world.Map.FindRegion("palmerway") != null)
        {
            List<TileCoord> path = Pathfinder.FindPath(world.Map, 0, new TileCoord(88, 45), new TileCoord(104, 47));
            Assert.Equal(new TileCoord(104, 47), path[^1]);
        }
    }

    [Fact]
    public void EveryShippedMapPlacesOnlyRealNpcsAndObjectsOnOpenGround()
    {
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        string[] maps = Directory.GetFiles(Path.Combine(Repo.ServerDirectory, "data", "maps"), "*.rthmap");
        Assert.Contains(maps, m => Path.GetFileName(m) == "tolling.rthmap");
        foreach (string path in maps)
        {
            MapData map = MapData.Load(path);
            MapPlacements placements = MapPlacements.Load(path);
            string name = Path.GetFileName(path);
            Assert.False(map.IsBlocked(map.SpawnPlane, map.Spawn.X, map.Spawn.Y), name + ": the spawn is blocked");
            foreach (NpcSpawn spawn in placements.Npcs)
            {
                Assert.True(content.Npcs.ContainsKey(spawn.Npc), name + ": unknown NPC " + spawn.Npc);
                Assert.False(map.IsBlocked(spawn.Plane, spawn.X, spawn.Y), name + ": " + spawn.Npc + " spawns on a blocked tile (" + spawn.X + ", " + spawn.Y + ")");
            }
            foreach (PlacedObject o in placements.Objects)
                Assert.True(content.Objects.ContainsKey(o.Type), name + ": unknown object " + o.Type);
        }
    }

    [Fact]
    public void ValidationReportsEveryProblem()
    {
        ContentParts parts = TestContent.Parts();
        parts.Abilities.Add(new AbilityDef { Id = "spark", Name = "Dupe", DamageMin = 5, DamageMax = 1 });
        parts.AutoAttack = "spark";
        parts.ActionBar = new List<string> { "missing" };
        parts.Items.Add(new ItemDef { Id = "bad", Name = "Bad", Equipment = new EquipmentDef { Stats = { ["strength"] = 5 } } });
        parts.Items.Add(new ItemDef { Id = "pot", Name = "Pot", Consumable = new ConsumableDef { Leaves = "nothing", Effects = { new EffectOp { Op = "teleport" } } } });
        parts.DropTables.Add(new DropTableDef { Id = "loop", Entries = { new DropEntry { Table = "loop" } } });
        parts.DropTables.Add(new DropTableDef { Id = "ghost", Entries = { new DropEntry { Item = "unobtainium" } } });
        parts.World.StarterEquipment.Add("gem");

        var e = Assert.Throws<ContentException>(() => GameContent.Create(parts));
        Assert.Contains("Duplicate ability id 'spark'", e.Message);
        Assert.Contains("damage range is invalid", e.Message);
        Assert.Contains("must have type Auto", e.Message);
        Assert.Contains("'missing' doesn't exist", e.Message);
        Assert.Contains("unknown stat 'strength'", e.Message);
        Assert.Contains("unknown effect op 'teleport'", e.Message);
        Assert.Contains("leaves 'nothing'", e.Message);
        Assert.Contains("'loop': refers back to itself", e.Message);
        Assert.Contains("item 'unobtainium' doesn't exist", e.Message);
        Assert.Contains("Starter equipment 'gem'", e.Message);
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(1, 1, 1, 0, 0)]
    [InlineData(50, 10, 5, 0, 0)]
    [InlineData(100, 20, 10, 0, 0)]
    [InlineData(-1, 0, 0, 1, 1)]
    [InlineData(-50, 0, 0, 10, 5)]
    [InlineData(-100, 0, 0, 20, 10)]
    public void AnyLeanOfBileBringsBothOfItsPoleStatusesScalingToTheLimit(int bile, int infused, int choleric, int warding, int melancholic)
    {
        var rules = new CombatRules();
        Assert.Equal(infused, rules.InfusedPercent(bile));
        Assert.Equal(choleric, rules.CholericPercent(bile));
        Assert.Equal(warding, rules.WardingPercent(bile));
        Assert.Equal(melancholic, rules.MelancholicPercent(bile));
    }

    [Fact]
    public void AbilityRequirementsExplainThemselves()
    {
        var flare = new AbilityDef { Name = "Flare", PhlegmRequired = 50, BileMin = 30 };
        Assert.Equal("Flare needs 50% phlegm.", flare.CheckRequirements(10, 40));
        Assert.Equal("Flare needs your bile at +30 or higher.", flare.CheckRequirements(60, 0));
        Assert.Null(flare.CheckRequirements(60, 30));

        var shroud = new AbilityDef { Name = "Shroud", BileMax = -30 };
        Assert.Equal("Shroud needs your bile at -30 or lower.", shroud.CheckRequirements(0, 0));
    }

    [Fact]
    public void LineOfSightIsBlockedByWallsButNotByRocks()
    {
        MapData map = Maps.Open(20, 20, new TileCoord(0, 0));
        map.Block(0, 5, 5);
        Assert.True(LineOfSight.Has(map, 0, new TileCoord(2, 5), new TileCoord(8, 5)));

        map.AddWall(0, 6, 5, WallSide.East);
        Assert.False(LineOfSight.Has(map, 0, new TileCoord(2, 5), new TileCoord(8, 5)));
        Assert.True(LineOfSight.Has(map, 0, new TileCoord(2, 7), new TileCoord(8, 7)));
        Assert.True(LineOfSight.Has(map, 0, new TileCoord(3, 3), new TileCoord(3, 3)));
    }

    [Fact]
    public void VersionOneSavesLoadWithFullBloodAndDevotion()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rth-v1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "old.json"), "{ \"Version\": 1, \"Name\": \"Old\", \"Plane\": 0, \"X\": 7, \"Y\": 6 }");
            var fx = new WorldFixture(Maps.Open(20, 20, new TileCoord(1, 1)));
            PlayerSave save = new JsonPlayerStore(dir).Load("old")!;
            fx.Store.Save("old", save);
            fx.Login("Old");
            var old = fx.Player("Old");
            // Positions are absolute world tiles, so even the oldest saves keep theirs.
            Assert.Equal((new TileCoord(7, 6), old.MaxBlood, (double)old.MaxDevotion, 0), (old.Position, old.Blood, old.Devotion, old.Bile));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void EveryMessageRoundTrips()
    {
        var messages = new Message[]
        {
            new LoginMessage { Name = "Ann" },
            new WalkToMessage { X = -3, Y = 9, Run = true },
            new LogoutMessage(),
            new AttackMessage { TargetId = 42 },
            new UseAbilityMessage { AbilityId = "gall_spark" },
            new SetHymnMessage { HymnId = "bulwark", On = true },
            new LoginOkMessage { PlayerId = 7, TickMillis = 600 },
            new LoginRejectedMessage { Reason = "no" },
            new WorldUpdateMessage
            {
                Tick = 99,
                Added = { new EntityAdded { Id = 1, Kind = EntityKind.Npc, Name = "Rat", Position = new TileCoord(1, 2), Shape = EntityShape.Box, Color = 0x123456, Scale = 6, HealthPercent = 40, Held = "sword" } },
                Appearances = { new AppearanceEvent { Id = 1, Held = "mace", Overhead = "bulwark" } },
                Moved = { new EntityMoved { Id = 1, Running = true, Steps = { new TileCoord(2, 2), new TileCoord(3, 2) } } },
                Removed = { 5 },
                Health = { new HealthEvent { Id = 1, Percent = 30 } },
                Hits = { new HitEvent { TargetId = 1, SourceId = 2, Amount = 77, Kind = HitKind.Deflected } },
                Actions = { new ActionEvent { SourceId = 2, TargetId = 1, AbilityId = "x", Pole = -1 }, new ActionEvent { SourceId = 2, TargetId = 2, AbilityId = "anim:work", Face = new TileCoord(-3, 9) } },
                Deaths = { 1 },
                ObjectEffects = { new ObjectEffectEvent { ObjectType = "holy_symbol", Plane = 0, X = 1037, Y = 1053, Effect = "flare", Arg = "colour=#ff0000;delay=0.7" } },
            },
            new PlayerStateMessage
            {
                Tick = 5, Blood = 900, MaxBlood = 1000, Phlegm = 55, Bile = -42, Devotion = 10, MaxDevotion = 300,
                Hymns = { "bulwark", "zeal" }, InCombat = true, TargetId = 3, GlobalCooldownReadyTick = 8, QueuedAbility = "q", StalledAbility = "s",
                Cooldowns = { new CooldownState { AbilityId = "a", ReadyTick = 20 } },
                Statuses = { new StatusState { Id = "ward", Magnitude = 30, EndTick = 15 }, new StatusState { Id = "infused", Magnitude = 4 } },
            },
            TestContent.Create().ToMessage(),
            new GameTextMessage { Kind = TextKind.Ferryman, Text = "hello" },
            new ChatMessage { Channel = ChatChannel.Public, Text = "hi" },
            new ChatLineMessage { Channel = ChatChannel.Public, EntityId = 7, From = "Alice", Text = "hi" },
            new QueryMessage { Kind = QueryKinds.Inspect, Arg = "7" },
            new QueryResultMessage { Kind = QueryKinds.Inspect, Arg = "7", Json = "{\"Name\":\"Alice\"}" },
            new QueryResultMessage { Kind = QueryKinds.Inspect, Arg = "8", Problem = "They are too far away." },
            new ItemActionMessage { Container = ContainerKind.Equipment, Slot = 3, ActionId = "unequip" },
            new MoveItemMessage { From = 2, To = 27 },
            new PickupMessage { GroundItemId = 99 },
            new ContainerMessage { Kind = ContainerKind.Inventory, Slots = { new ItemSlot { ItemId = "bread", Count = 1 }, new ItemSlot { ItemId = "", Count = 0 } } },
            new StatsMessage { Stats = { ["ward"] = 5, ["abilityDamage"] = 110 } },
            new WorldUpdateMessage { Tick = 1, GroundAdded = { new GroundItemAdded { Id = 4, ItemId = "coins", Count = 7, Position = new TileCoord(3, 4) } }, GroundRemoved = { 2 } },
        };

        foreach (Message original in messages)
        {
            byte[] frame = MessageCodec.EncodeFrame(original);
            Message decoded = MessageCodec.Decode(frame.Skip(4).ToArray(), frame.Length - 4);
            Assert.Equal(original.Type, decoded.Type);
            // Re-encoding the decoded message must give identical bytes.
            Assert.Equal(frame, MessageCodec.EncodeFrame(decoded));
        }
    }
}

internal static class Repo
{
    public static string ServerDirectory
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "server", "data", "content");
                if (Directory.Exists(candidate)) return Path.Combine(dir.FullName, "server");
            }
            throw new DirectoryNotFoundException("Couldn't find server/data/content above " + AppContext.BaseDirectory);
        }
    }

    public static string ContentDirectory => Path.Combine(ServerDirectory, "data", "content");
}
