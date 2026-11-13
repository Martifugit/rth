using Newtonsoft.Json;
using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>Creatures' special attacks: chosen, wound up with a telegraph, landed on whoever stayed in them.</summary>
public class NpcMoveTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    /// <summary>A sturdy creature whose ordinary blow does 1, with the given moves.</summary>
    private static WorldFixture Arena(TileCoord at, params NpcMoveDef[] moves)
    {
        ContentParts parts = TestContent.Parts();
        parts.Npcs.Add(new NpcDef
        {
            Id = "golem", Name = "Golem", Blood = 10000, Style = CombatStyle.Melee, AttackRange = 1, AttackTicks = 4,
            MaxHit = 1, MinHit = 1, Accuracy = 1.0, Evasion = 0, WanderRadius = 0, LeashRadius = 30, Aggressive = true, AggroRadius = 6,
            Moves = moves.ToList(),
        });
        return new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig(), GameContent.Create(parts),
            new[] { new NpcSpawn { Npc = "golem", X = at.X, Y = at.Y, Wander = 0 } });
    }

    private static TelegraphsState? LastTelegraphs(FakeConnection c)
    {
        JsonStateMessage? m = c.Sent.OfType<JsonStateMessage>().LastOrDefault(s => s.Key == TelegraphsState.Key);
        return m == null ? null : JsonConvert.DeserializeObject<TelegraphsState>(m.Json);
    }

    /// <summary>Ticks until the creature starts its move (its telegraph appears); fails after a while.</summary>
    private static Telegraph AwaitTelegraph(WorldFixture fx, FakeConnection c)
    {
        for (int i = 0; i < 20; i++)
        {
            fx.Ticks(1);
            Telegraph? t = LastTelegraphs(c)?.Telegraphs.FirstOrDefault();
            if (t != null) return t;
        }
        throw new Xunit.Sdk.XunitException("no telegraph appeared");
    }

    private static NpcMoveDef Slam() => new()
    {
        Id = "slam", Name = "Slam", Area = NpcMoveDef.Areas.TargetTile, Radius = 1, WindupTicks = 3, CooldownTicks = 30,
        MinHit = 100, MaxHit = 100, Message = "The golem raises its fists!",
    };

    [Fact]
    public void ASlamTelegraphsThenLandsOnWhoeverStaysInIt()
    {
        var fx = Arena(new TileCoord(6, 5), Slam());
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        int before = alice.Blood;

        Telegraph t = AwaitTelegraph(fx, client);
        Assert.Equal("slam", t.Move);
        Assert.Equal((5, 5), (t.X, t.Y));
        Assert.Equal(1, t.Radius);
        Assert.Equal(t.StartTick + 3, t.LandTick);
        Assert.Contains("The golem raises its fists!", client.Texts);
        int atStart = alice.Blood;

        fx.Ticks(2);
        Assert.Equal(atStart, alice.Blood); // still winding up
        fx.Ticks(1);
        Assert.Equal(atStart - 100, alice.Blood);
        Assert.True(before - alice.Blood >= 100);
        Assert.Empty(LastTelegraphs(client)!.Telegraphs); // landed: the marker goes
    }

    [Fact]
    public void SteppingOutOfTheSquareDodgesIt()
    {
        var fx = Arena(new TileCoord(6, 5), Slam());
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        AwaitTelegraph(fx, client);
        int atStart = alice.Blood;

        client.FromClient(new WalkToMessage { X = 2, Y = 5 });
        fx.Ticks(3);
        Assert.True(alice.Position.ChebyshevDistance(new TileCoord(5, 5)) > 1);
        Assert.True(alice.Blood >= atStart); // nothing landed (it may have regenerated a little)
    }

    [Fact]
    public void AHeavyBlowMissesATargetThatSteppedAway()
    {
        var blow = new NpcMoveDef { Id = "haymaker", Name = "Haymaker", WindupTicks = 2, CooldownTicks = 30, MinHit = 300, MaxHit = 300 };
        var fx = Arena(new TileCoord(6, 5), blow);
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Telegraph t = AwaitTelegraph(fx, client);
        Assert.Equal(alice.Id, t.TargetId); // it follows the target
        int atStart = alice.Blood;
        client.FromClient(new WalkToMessage { X = 3, Y = 5 });
        fx.Ticks(2);
        Assert.True(alice.Blood > atStart - 300);
    }

    [Fact]
    public void ASweepHitsEveryoneAroundTheCreature()
    {
        var sweep = new NpcMoveDef { Id = "sweep", Name = "Sweep", Area = NpcMoveDef.Areas.Self, Radius = 1, WindupTicks = 1, CooldownTicks = 30, MinHit = 50, MaxHit = 50 };
        var fx = Arena(new TileCoord(30, 30), sweep); // out of reach until both are in
        FakeConnection a = fx.Login("Alice");
        fx.Login("Bob");
        Player alice = fx.Player("Alice");
        Player bob = fx.Player("Bob");
        bob.Position = new TileCoord(7, 6);
        bob.Teleported = true;
        Npc golem = fx.Npc("golem");
        golem.Position = new TileCoord(6, 5);
        golem.Teleported = true;
        Telegraph t = AwaitTelegraph(fx, a);
        Assert.Equal((6, 5), (t.X, t.Y));
        int aliceBefore = alice.Blood, bobBefore = bob.Blood;
        fx.Ticks(1);
        Assert.Equal(aliceBefore - 50, alice.Blood);
        Assert.Equal(bobBefore - 50, bob.Blood);
    }

    [Fact]
    public void AnEnrageWaitsUntilItsBloodIsLow()
    {
        var enrage = new NpcMoveDef { Id = "frenzy", Name = "Frenzy", BelowBloodPercent = 50, CooldownTicks = 30, MinHit = 70, MaxHit = 70 };
        var fx = Arena(new TileCoord(6, 5), enrage);
        FakeConnection client = fx.Login("Alice");
        Npc golem = fx.Npc("golem");
        fx.Ticks(12);
        Assert.False(fx.World.Get<NpcSystem>().MovesOf(golem).ReadyAt.ContainsKey("frenzy"));

        golem.Blood = golem.MaxBlood / 3;
        int before = fx.Player("Alice").Blood;
        fx.Ticks(5);
        Assert.True(fx.World.Get<NpcSystem>().MovesOf(golem).ReadyAt.ContainsKey("frenzy"));
        Assert.True(fx.Player("Alice").Blood <= before - 70 + 10); // landed (less a little regeneration)
        Assert.NotNull(client);
    }

    [Fact]
    public void AMendHappensOnceAFight()
    {
        var mend = new NpcMoveDef { Id = "mend", Name = "Mend", BelowBloodPercent = 50, OncePerFight = true, CooldownTicks = 1, HealPercent = 20 };
        var fx = Arena(new TileCoord(6, 5), mend);
        fx.Login("Alice");
        Npc golem = fx.Npc("golem");
        golem.Blood = 1000;
        fx.Ticks(6);
        Assert.Contains("mend", fx.World.Get<NpcSystem>().MovesOf(golem).UsedThisFight);
        int mended = golem.Blood;
        Assert.InRange(mended, 2500, 3000); // 20% (2000) once, less what Alice hit back with
        fx.Ticks(20);
        Assert.True(golem.Blood <= mended); // never again this fight
    }

    [Fact]
    public void ContentRejectsAMoveWithABadArea()
    {
        ContentParts parts = TestContent.Parts();
        parts.Npcs.Add(new NpcDef { Id = "odd", Name = "Odd", Moves = { new NpcMoveDef { Id = "x", Area = "everywhere", MaxHit = 5 } } });
        var e = Assert.Throws<ContentException>(() => GameContent.Create(parts));
        Assert.Contains("area must be one of", e.Message);
    }
}
