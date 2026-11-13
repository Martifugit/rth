using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>Group fights: threat decides who a creature fights, damage decides who gets its loot.</summary>
public class ThreatTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static NpcSpawn At(string npc, int x, int y) => new() { Npc = npc, X = x, Y = y, Wander = 0 };

    /// <summary>The test content plus a tough creature that fights back gently, and a taunt.</summary>
    private static GameContent Content()
    {
        ContentParts parts = TestContent.Parts();
        parts.Npcs.Add(new NpcDef { Id = "ogre", Name = "Ogre", Blood = 100000, Style = CombatStyle.Melee, AttackRange = 1, AttackTicks = 2, MaxHit = 1, MinHit = 1, Accuracy = 1.0, Evasion = 0, WanderRadius = 0, LeashRadius = 30 });
        parts.Abilities.Add(new AbilityDef { Id = "challenge", Name = "Challenge", Type = AbilityType.Builder, CooldownTicks = 3, Range = 8, DamageMin = 1, DamageMax = 1, TauntTicks = 10 });
        return GameContent.Create(parts);
    }

    private static WorldFixture Arena(params NpcSpawn[] spawns) =>
        new(Maps.Open(40, 40, Spawn), new ServerConfig(), Content(), spawns);

    [Fact]
    public void ACreatureTurnsOnWhoeverHurtsItMost()
    {
        var fx = Arena(At("ogre", 8, 5));
        FakeConnection alice = fx.Login("Alice");
        FakeConnection bob = fx.Login("Bob");
        Npc ogre = fx.Npc("ogre");

        alice.FromClient(new AttackMessage { TargetId = ogre.Id });
        fx.Ticks(2);
        Assert.Equal(fx.Player("Alice").Id, ogre.TargetId); // first blow, first grudge

        // Bob hits far harder (spark is 100 to the auto-attack's 50, every global cooldown).
        bob.FromClient(new AttackMessage { TargetId = ogre.Id });
        for (int i = 0; i < 6; i++)
        {
            bob.FromClient(new UseAbilityMessage { AbilityId = i % 2 == 0 ? "spark" : "draught" });
            fx.Ticks(3);
        }
        Assert.Equal(fx.Player("Bob").Id, ogre.TargetId);
    }

    [Fact]
    public void ATauntTakesTheCreatureAtOnce()
    {
        var fx = Arena(At("ogre", 8, 5));
        FakeConnection alice = fx.Login("Alice");
        FakeConnection bob = fx.Login("Bob");
        Npc ogre = fx.Npc("ogre");

        alice.FromClient(new AttackMessage { TargetId = ogre.Id });
        fx.Ticks(10);
        Assert.Equal(fx.Player("Alice").Id, ogre.TargetId);

        bob.FromClient(new AttackMessage { TargetId = ogre.Id });
        bob.FromClient(new UseAbilityMessage { AbilityId = "challenge" });
        fx.Ticks(1);
        Assert.Equal(fx.Player("Bob").Id, ogre.TargetId);

        // Alice keeps hitting harder, but the taunt holds it for its ten ticks...
        fx.Ticks(8);
        Assert.Equal(fx.Player("Bob").Id, ogre.TargetId);
        // ...and once it lapses, clearly more threat takes it back.
        bob.FromClient(new WalkToMessage { X = 2, Y = 2 });
        fx.Ticks(20);
        Assert.Equal(fx.Player("Alice").Id, ogre.TargetId);
    }

    [Fact]
    public void TheLootGoesToWhoeverDidTheMostDamage()
    {
        var fx = Arena(At("rat", 8, 5));
        FakeConnection alice = fx.Login("Alice");
        FakeConnection bob = fx.Login("Bob");
        Npc rat = fx.Npc("rat");
        CombatSystem combat = fx.World.Get<CombatSystem>();

        // Bob takes it most of the way; Alice lands the last blow.
        combat.Damage(rat, 250, fx.Player("Bob"), HitKind.Normal);
        combat.Damage(rat, 50, fx.Player("Alice"), HitKind.Normal);
        Assert.True(rat.IsDead);

        GroundItem coins = Assert.Single(fx.World.Get<GroundItemSystem>().Items);
        Assert.Equal(fx.Player("Bob").Key, coins.OwnerKey);
    }

    [Fact]
    public void EveryoneWhoHelpedGetsTheKill()
    {
        var fx = Arena(At("rat", 8, 5));
        fx.Login("Alice");
        fx.Login("Bob");
        fx.Login("Carol");
        Npc rat = fx.Npc("rat");
        var credited = new List<string>();
        fx.World.Events.Subscribe<EntityDied>(e => credited.AddRange(e.Credited.Select(p => p.Name)));
        CombatSystem combat = fx.World.Get<CombatSystem>();
        combat.Damage(rat, 100, fx.Player("Bob"), HitKind.Normal);
        combat.Damage(rat, 200, fx.Player("Alice"), HitKind.Normal);
        Assert.Equal(new[] { "Alice", "Bob" }, credited); // the last blow first; Carol never struck
        Assert.Equal(1, fx.World.Get<BestiarySystem>().Entry(fx.Player("Bob"), "rat").Kills);
    }

    [Fact]
    public void ACreatureThatResetsForgetsEveryone()
    {
        var fx = Arena(At("ogre", 8, 5));
        FakeConnection alice = fx.Login("Alice");
        Npc ogre = fx.Npc("ogre");
        alice.FromClient(new AttackMessage { TargetId = ogre.Id });
        fx.Ticks(4);
        ThreatSystem threat = fx.World.Get<ThreatSystem>();
        Assert.NotEmpty(threat.Of(ogre).Threat);

        // Run far away: it leashes home and forgets.
        Player a = fx.Player("Alice");
        alice.FromClient(new WalkToMessage { X = 39, Y = 39 });
        fx.Ticks(60);
        Assert.Equal(0, ogre.TargetId);
        Assert.Empty(threat.Of(ogre).Threat);
        Assert.NotNull(a);
    }

    [Fact]
    public void AGroupSeesItsMembersLoot()
    {
        var fx = Arena(At("rat", 8, 5));
        FakeConnection alice = fx.Login("Alice");
        FakeConnection bob = fx.Login("Bob");
        fx.Login("Carol");
        alice.FromClient(new ChatMessage { Text = "/invite bob" });
        fx.Ticks(1);
        bob.FromClient(new ChatMessage { Text = "/accept" });
        fx.Ticks(1);
        fx.World.Get<CombatSystem>().Damage(fx.Npc("rat"), 300, fx.Player("Alice"), HitKind.Normal);
        GroundItemSystem ground = fx.World.Get<GroundItemSystem>();
        GroundItem coins = Assert.Single(ground.Items);
        Assert.True(ground.IsVisibleTo(coins, fx.Player("Bob")));
        Assert.False(ground.IsVisibleTo(coins, fx.Player("Carol")));
    }

    [Fact]
    public void ArmourEvasionLowersACreaturesChanceToHit()
    {
        var fx = Arena(At("ogre", 8, 5));
        fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Npc ogre = fx.Npc("ogre");
        Assert.Equal(1.0, NpcSystem.HitChance(ogre, alice), 3);
        alice.Stats.Add(StatIds.Evasion, 30);
        Assert.Equal(0.7, NpcSystem.HitChance(ogre, alice), 3);
        alice.Stats.Add(StatIds.Evasion, 500);
        Assert.Equal(0.05, NpcSystem.HitChance(ogre, alice), 3); // never hopeless
    }
}
