using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>Stalling, optional ability queueing and Revolution.</summary>
public class AbilityFlowTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static WorldFixture Arena(params NpcSpawn[] spawns) =>
        new(Maps.Open(40, 40, Spawn), new ServerConfig(), null, spawns);

    private static NpcSpawn At(string npc, int x, int y) => new() { Npc = npc, X = x, Y = y };

    private static void Set(FakeConnection client, string key, string value) =>
        client.FromClient(new SetClientStateMessage { Key = key, Json = value });

    [Fact]
    public void AnAbilityUsedOutOfRangeStallsAndFiresOnArrival()
    {
        var fx = Arena(At("dummy", 20, 5)); // 15 tiles away; arrives in range (8) after 7 ticks
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Npc dummy = fx.Npc("dummy");
        client.FromClient(new AttackMessage { TargetId = dummy.Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(1);

        Assert.Equal("spark", alice.StalledAbility);
        Assert.Null(alice.QueuedAbility);
        Assert.True(alice.Cooldowns["spark"] > fx.World.CurrentTick); // its cooldown already runs
        Assert.True(alice.GlobalCooldownReadyTick > fx.World.CurrentTick);
        Assert.True(fx.World.Get<CombatSystem>().InCombat(alice)); // engaged, though nothing has been hit yet

        fx.Ticks(5);
        Assert.Equal(dummy.MaxBlood, dummy.Blood); // still running
        Assert.Equal(0, alice.Bile); // nothing spent until it fires

        fx.Ticks(1);
        Assert.Equal(new TileCoord(12, 5), alice.Position);
        Assert.Null(alice.StalledAbility);
        Assert.Equal(dummy.MaxBlood - 100, dummy.Blood); // spark, and no auto-attack on top
        Assert.Equal(10, alice.Bile);
    }

    [Fact]
    public void AStallReleasedAfterTheGlobalCooldownLetsTheNextAbilityFireTheSameTick()
    {
        var fx = Arena(At("dummy", 20, 5));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Npc dummy = fx.Npc("dummy");
        client.FromClient(new AttackMessage { TargetId = dummy.Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(4); // stalled on the first tick; the global cooldown has passed, still out of range

        client.FromClient(new UseAbilityMessage { AbilityId = "draught" });
        fx.Ticks(1);
        Assert.Equal("spark", alice.StalledAbility); // only one stall: draught waits for arrival
        Assert.Equal("draught", alice.QueuedAbility);

        fx.Ticks(2); // arrives
        // Both on one tick: spark, then draught with spark's +10 bile infusing it.
        Assert.Equal(dummy.MaxBlood - 100 - (100 + fx.World.Rules.InfusedPercent(10)), dummy.Blood);
        Assert.Equal(20, alice.Phlegm);
        Assert.Equal(0, alice.Bile); // +10 then -10
        Assert.Null(alice.StalledAbility);
        Assert.Null(alice.QueuedAbility);
    }

    [Fact]
    public void WalkingAwayKeepsTheTargetAndStallUntilDroppingOutOfCombat()
    {
        var fx = Arena(At("dummy", 20, 5));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Npc dummy = fx.Npc("dummy");
        client.FromClient(new AttackMessage { TargetId = dummy.Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(1);
        Assert.Equal("spark", alice.StalledAbility);

        client.FromClient(new WalkToMessage { X = 5, Y = 5 });
        fx.Ticks(1);
        Assert.Null(alice.Interaction); // no longer chasing...
        Assert.Equal(dummy.Id, alice.TargetId); // ...but still fighting it
        Assert.Equal("spark", alice.StalledAbility);

        fx.Ticks(fx.World.Rules.CombatTimeoutTicks);
        Assert.Equal(0, alice.TargetId);
        Assert.Null(alice.StalledAbility);
        Assert.True(alice.Cooldowns["spark"] > 0); // its cooldown was spent all the same
        Assert.Contains(client.Texts, t => t.Contains("Spark fades unused"));
    }

    [Fact]
    public void AnAbilityAfterWalkingAwayTakesTheFightBackUp()
    {
        var fx = Arena(At("dummy", 8, 5));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Npc dummy = fx.Npc("dummy");
        client.FromClient(new AttackMessage { TargetId = dummy.Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(1);
        int afterSpark = dummy.Blood;
        Assert.True(afterSpark < dummy.MaxBlood);

        // Walk well out of range, keeping the target.
        client.FromClient(new WalkToMessage { X = 2, Y = 30, Run = true });
        fx.Ticks(6); // inside the combat timeout, and still in sight
        Assert.Equal(dummy.Id, alice.TargetId);
        Assert.False(fx.World.Get<CombatSystem>().InRange(alice, dummy, 8));

        // No new click on the dummy: the ability alone stalls, runs back and fires on arrival.
        client.FromClient(new UseAbilityMessage { AbilityId = "draught" });
        fx.Ticks(1);
        Assert.IsType<AttackInteraction>(alice.Interaction);
        Assert.Equal("draught", alice.StalledAbility);
        for (int i = 0; i < 30 && alice.StalledAbility != null; i++) fx.Ticks(1);
        Assert.Null(alice.StalledAbility);
        Assert.True(dummy.Blood < afterSpark);
    }

    [Fact]
    public void AttackingSomethingElseSwitchesTheTarget()
    {
        var fx = Arena(At("dummy", 8, 5), At("rat", 8, 8));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        fx.Ticks(1);
        client.FromClient(new AttackMessage { TargetId = fx.Npc("rat").Id });
        fx.Ticks(1);
        Assert.Equal(fx.Npc("rat").Id, alice.TargetId);
    }

    /// <summary>Test content plus "brace": a self-ward that keeps a stall.</summary>
    private static GameContent WithBrace()
    {
        List<AbilityDef> abilities = TestContent.Abilities();
        abilities.Add(new AbilityDef { Id = "brace", Name = "Brace", Type = AbilityType.Builder, CooldownTicks = 6, KeepsStall = true, Effect = EffectType.Ward, EffectMagnitude = 10, EffectTicks = 5 });
        ContentParts parts = TestContent.Parts(abilities: abilities);
        parts.ActionBar.Add("brace");
        return GameContent.Create(parts);
    }

    [Fact]
    public void AKeepsStallAbilityFiresMidStallWithoutReleasingIt()
    {
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig(), WithBrace(), new[] { At("dummy", 20, 5) });
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Npc dummy = fx.Npc("dummy");
        client.FromClient(new AttackMessage { TargetId = dummy.Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(3);
        client.FromClient(new UseAbilityMessage { AbilityId = "brace" }); // global cooldown ends this tick
        fx.Ticks(1);

        Assert.True(alice.Cooldowns.ContainsKey("brace"));
        Assert.Equal(EffectType.Ward, Assert.Single(alice.Effects).Type);
        Assert.Equal("spark", alice.StalledAbility); // still held
        Assert.Equal(fx.World.CurrentTick, alice.LastCombatTick); // and it kept her in combat
        fx.Ticks(3);
        Assert.Equal(dummy.MaxBlood - 100, dummy.Blood); // spark fired on arrival
    }

    [Fact]
    public void AKeepsStallAbilityHoldsAStallInCombatWithNoTarget()
    {
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig(), WithBrace(), new[] { At("dummy", 20, 5) });
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(1);
        alice.LastCombatTick = fx.World.CurrentTick;
        client.FromClient(new WalkToMessage { X = 5, Y = 5 });
        fx.Ticks(8);
        client.FromClient(new UseAbilityMessage { AbilityId = "brace" }); // just before the combat timer runs out
        fx.Ticks(1);
        Assert.True(alice.Cooldowns.ContainsKey("brace"));

        fx.Ticks(fx.World.Rules.CombatTimeoutTicks - 2);
        Assert.Equal("spark", alice.StalledAbility); // it would have faded without the brace
        fx.Ticks(2);
        Assert.Null(alice.StalledAbility);
    }

    [Fact]
    public void KeepsStallAbilitiesCantDealDamage()
    {
        List<AbilityDef> abilities = TestContent.Abilities();
        abilities.Add(new AbilityDef { Id = "bad", Name = "Bad", Type = AbilityType.Builder, KeepsStall = true, DamageMin = 10, DamageMax = 10 });
        var ex = Assert.Throws<ContentException>(() => TestContent.Create(abilities: abilities));
        Assert.Contains("keepsStall", ex.Message);
    }

    [Fact]
    public void AStallCarriesOverToANewTarget()
    {
        var fx = Arena(At("dummy", 20, 5), At("dummy", 5, 20));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Npc east = fx.World.Npcs.First(n => n.Position == new TileCoord(20, 5));
        Npc north = fx.World.Npcs.First(n => n.Position == new TileCoord(5, 20));
        client.FromClient(new AttackMessage { TargetId = east.Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(1);

        client.FromClient(new AttackMessage { TargetId = north.Id });
        fx.Ticks(1);
        Assert.Equal("spark", alice.StalledAbility);
        fx.Ticks(10);
        Assert.Equal(east.MaxBlood, east.Blood);
        Assert.True(north.Blood < north.MaxBlood);
        Assert.Equal(10, alice.Bile);
    }

    [Fact]
    public void WithQueueingOffAnAbilityPressedDuringTheGlobalCooldownIsIgnored()
    {
        var fx = Arena(At("dummy", 8, 5));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Npc dummy = fx.Npc("dummy");
        Set(client, CombatOptions.QueueingKey, "false");
        client.FromClient(new AttackMessage { TargetId = dummy.Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(1);

        client.FromClient(new UseAbilityMessage { AbilityId = "draught" }); // global cooldown runs 2 more ticks
        fx.Ticks(1);
        Assert.Null(alice.QueuedAbility);
        fx.Ticks(3);
        Assert.Equal(10, alice.Bile); // draught never fired
        Assert.False(alice.Cooldowns.ContainsKey("draught"));

        client.FromClient(new UseAbilityMessage { AbilityId = "draught" }); // pressed when ready: fires
        fx.Ticks(1);
        Assert.Equal(0, alice.Bile);
    }

    [Fact]
    public void WithQueueingOnAnAbilityPressedDuringTheGlobalCooldownFiresWhenItEnds()
    {
        var fx = Arena(At("dummy", 8, 5));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(1);
        client.FromClient(new UseAbilityMessage { AbilityId = "draught" });
        fx.Ticks(1);
        Assert.Equal("draught", alice.QueuedAbility);
        fx.Ticks(2);
        Assert.Null(alice.QueuedAbility);
        Assert.Equal(0, alice.Bile);
    }

    [Fact]
    public void RevolutionFiresTheLeftmostReadyBuilderOnTheMainBar()
    {
        var fx = Arena(At("dummy", 8, 5));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Set(client, CombatOptions.RevolutionKey, "builders");
        // Items and focuses on the bar are skipped; the spender is skipped in builders mode.
        Set(client, CombatOptions.BarsKey, "[[\"i:bread\",\"h:bulwark\",\"a:flare\",\"a:draught\",\"a:spark\"],[],[]]");
        alice.Phlegm = 60;
        alice.Bile = 40;
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        fx.Ticks(1);
        Assert.Equal(30, alice.Bile); // draught (-10), not flare
        Assert.False(alice.Cooldowns.ContainsKey("flare"));

        fx.Ticks(3);
        Assert.Equal(40, alice.Bile); // then spark (+10), the next one ready
        Assert.True(alice.Cooldowns.ContainsKey("spark"));
    }

    [Fact]
    public void RevolutionInAllModeFiresSpendersAndUltimatesToo()
    {
        var fx = Arena(At("dummy", 8, 5));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Set(client, CombatOptions.RevolutionKey, "all");
        Set(client, CombatOptions.BarsKey, "[[\"a:ult\",\"a:spark\"]]");
        alice.Phlegm = 100;
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        fx.Ticks(1);
        Assert.True(alice.Cooldowns.ContainsKey("ult"));
        Assert.Equal(0, alice.Phlegm);
    }

    [Fact]
    public void RevolutionIsOffByDefaultAndManualPressesComeFirst()
    {
        var fx = Arena(At("dummy", 8, 5));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        fx.Ticks(6);
        Assert.Empty(alice.Cooldowns); // manual: only auto-attacks

        Set(client, CombatOptions.RevolutionKey, "builders");
        client.FromClient(new UseAbilityMessage { AbilityId = "draught" }); // spark is leftmost, but a press wins
        fx.Ticks(1);
        Assert.True(alice.Cooldowns.ContainsKey("draught"));
        Assert.False(alice.Cooldowns.ContainsKey("spark"));
    }

    [Fact]
    public void CombatOptionsReadTheirStoredValues()
    {
        Assert.True(CombatOptions.QueueingFrom(null));
        Assert.False(CombatOptions.QueueingFrom("false"));
        Assert.Equal(RevolutionMode.Manual, CombatOptions.RevolutionFrom(null));
        foreach (RevolutionMode mode in new[] { RevolutionMode.Manual, RevolutionMode.Builders, RevolutionMode.All })
            Assert.Equal(mode, CombatOptions.RevolutionFrom(CombatOptions.RevolutionValue(mode)));
        var builders = new CombatOptions { Revolution = RevolutionMode.Builders };
        Assert.True(builders.AutoCasts(AbilityType.Builder));
        Assert.False(builders.AutoCasts(AbilityType.Spender));
        Assert.False(builders.AutoCasts(AbilityType.Auto));
    }
}
