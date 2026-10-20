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
        Assert.NotEmpty(content.Assayer.First);
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
    [InlineData(0, 0, 0)]
    [InlineData(29, 0, 0)]
    [InlineData(30, 5, 0)]
    [InlineData(100, 20, 0)]
    [InlineData(-30, 0, 5)]
    [InlineData(-100, 0, 15)]
    [InlineData(-29, 0, 0)]
    public void BilePassivesStartAtTheThresholdAndScaleToTheLimit(int bile, int bonus, int ward)
    {
        var rules = new CombatRules();
        Assert.Equal(bonus, rules.CholericBonusPercent(bile));
        Assert.Equal(ward, rules.MelancholicWardPercent(bile));
    }

    [Fact]
    public void ExtremesBringFeverAndTorpor()
    {
        var rules = new CombatRules();
        Assert.True(rules.IsFevered(80));
        Assert.False(rules.IsFevered(79));
        Assert.True(rules.IsTorpid(-80));
        Assert.Equal(8, rules.PhlegmGainFor(8, 0));
        Assert.Equal(4, rules.PhlegmGainFor(8, -90));
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
    public void VersionOneSavesLoadWithFullBloodAndBreath()
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
            // Saves from before maps were recorded start at the spawn; everything else carries over.
            Assert.Equal((new TileCoord(1, 1), old.MaxBlood, old.MaxBreath, 0), (old.Position, old.Blood, old.Breath, old.Bile));
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
            new SetDeflectMessage { Style = CombatStyle.Occult },
            new LoginOkMessage { PlayerId = 7, TickMillis = 600 },
            new LoginRejectedMessage { Reason = "no" },
            new WorldUpdateMessage
            {
                Tick = 99,
                Added = { new EntityAdded { Id = 1, Kind = EntityKind.Npc, Name = "Rat", Position = new TileCoord(1, 2), Shape = EntityShape.Box, Color = 0x123456, Scale = 6, HealthPercent = 40 } },
                Moved = { new EntityMoved { Id = 1, Running = true, Steps = { new TileCoord(2, 2), new TileCoord(3, 2) } } },
                Removed = { 5 },
                Health = { new HealthEvent { Id = 1, Percent = 30 } },
                Hits = { new HitEvent { TargetId = 1, SourceId = 2, Amount = 77, Kind = HitKind.Deflected } },
                Actions = { new ActionEvent { SourceId = 2, TargetId = 1, AbilityId = "x", Pole = -1 } },
                Deaths = { 1 },
            },
            new PlayerStateMessage
            {
                Tick = 5, Blood = 900, MaxBlood = 1000, Phlegm = 55, Bile = -42, Breath = 10, MaxBreath = 300,
                Deflect = CombatStyle.Melee, InCombat = true, TargetId = 3, GlobalCooldownReadyTick = 8, QueuedAbility = "q",
                Cooldowns = { new CooldownState { AbilityId = "a", ReadyTick = 20 } },
                Effects = { new EffectState { Effect = EffectType.Ward, Magnitude = 30, EndTick = 15 } },
            },
            TestContent.Create().ToMessage(),
            new GameTextMessage { Kind = TextKind.Assayer, Text = "hello" },
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
