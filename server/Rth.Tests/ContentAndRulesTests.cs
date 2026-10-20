using Rth.Server.Content;
using Rth.Server.Persistence;
using Rth.Shared.Combat;
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
    public void TheShippedSpawnsAllNameRealNpcs()
    {
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        string spawns = GameContent.SpawnsPathFor(Path.Combine(Repo.ServerDirectory, "data", "maps", "test.rthmap"));
        Assert.True(File.Exists(spawns), "Export the test map from Unity to create " + spawns);
        foreach (NpcSpawn spawn in GameContent.LoadSpawns(spawns))
            Assert.Contains(spawn.Npc, content.Npcs.Keys);
    }

    [Fact]
    public void ValidationReportsEveryProblem()
    {
        List<AbilityDef> abilities = TestContent.Abilities();
        abilities.Add(new AbilityDef { Id = "spark", Name = "Dupe", DamageMin = 5, DamageMax = 1 });
        var e = Assert.Throws<ContentException>(() =>
            GameContent.Create(TestContent.Rules(), abilities, "spark", new[] { "missing" }, TestContent.Npcs()));
        Assert.Contains("Duplicate ability id 'spark'", e.Message);
        Assert.Contains("damage range is invalid", e.Message);
        Assert.Contains("must have type Auto", e.Message);
        Assert.Contains("'missing' doesn't exist", e.Message);
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
            Assert.Equal((new TileCoord(7, 6), old.MaxBlood, old.MaxBreath, 0), (old.Position, old.Blood, old.Breath, old.Bile));
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
