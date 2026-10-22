using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

public class CombatTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static WorldFixture Arena(params NpcSpawn[] spawns) => Arena(null, spawns);

    private static WorldFixture Arena(GameContent? content, params NpcSpawn[] spawns) =>
        new(Maps.Open(40, 40, Spawn), new ServerConfig(), content, spawns);

    private static NpcSpawn At(string npc, int x, int y) => new() { Npc = npc, X = x, Y = y };

    [Fact]
    public void AttackingWalksIntoRangeThenAutoAttacks()
    {
        var fx = Arena(At("dummy", 20, 5));
        FakeConnection client = fx.Login("Alice");
        Npc dummy = fx.Npc("dummy");

        client.FromClient(new AttackMessage { TargetId = dummy.Id });
        fx.Ticks(7); // 15 tiles away, range 8: walks 7 tiles

        Player alice = fx.Player("Alice");
        Assert.Equal(new TileCoord(12, 5), alice.Position);
        Assert.Equal(dummy.MaxBlood - 50, dummy.Blood);

        fx.Ticks(4); // the auto-attack comes round every 4 ticks
        Assert.Equal(dummy.MaxBlood - 100, dummy.Blood);
        Assert.Equal(new TileCoord(12, 5), alice.Position); // stood still while fighting
    }

    [Fact]
    public void BuildersBuildPhlegmAndShiftBile()
    {
        var fx = Arena(At("dummy", 8, 5));
        FakeConnection client = fx.Login("Alice");
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(1);

        Player alice = fx.Player("Alice");
        Assert.Equal(10, alice.Phlegm);
        Assert.Equal(10, alice.Bile);

        client.FromClient(new UseAbilityMessage { AbilityId = "draught" });
        fx.Ticks(3);
        Assert.Equal(20, alice.Phlegm);
        Assert.Equal(0, alice.Bile);
    }

    [Fact]
    public void TheGlobalCooldownSpacesAbilitiesThreeTicksApart()
    {
        var fx = Arena(At("dummy", 8, 5));
        FakeConnection client = fx.Login("Alice");
        Npc dummy = fx.Npc("dummy");
        client.FromClient(new AttackMessage { TargetId = dummy.Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(1);
        int afterFirst = dummy.Blood;

        client.FromClient(new UseAbilityMessage { AbilityId = "draught" });
        fx.Ticks(2);
        Assert.Equal(afterFirst, dummy.Blood); // still on the global cooldown
        fx.Ticks(1);
        Assert.Equal(afterFirst - (100 + fx.World.Rules.InfusedPercent(10)), dummy.Blood); // spark left bile at +10
    }

    [Fact]
    public void AnAbilityOnCooldownWaitsInTheQueue()
    {
        var fx = Arena(At("dummy", 8, 5));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(1);
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" }); // 5-tick cooldown
        fx.Ticks(3);
        Assert.Equal("spark", alice.QueuedAbility); // global cooldown is over, but spark's isn't
        Assert.Equal(10, alice.Bile);
        fx.Ticks(1);
        Assert.Equal("spark", alice.QueuedAbility);
        fx.Ticks(1);
        Assert.Null(alice.QueuedAbility);
        Assert.Equal(20, alice.Bile);
    }

    [Fact]
    public void SpendersNeedPhlegmAndBileAndSpendPhlegm()
    {
        var fx = Arena(At("dummy", 8, 5));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        fx.Ticks(1);

        client.FromClient(new UseAbilityMessage { AbilityId = "flare" });
        fx.Ticks(1);
        Assert.Contains("Flare needs 50% phlegm.", client.Texts);

        alice.Phlegm = 60;
        alice.Bile = 10;
        client.FromClient(new UseAbilityMessage { AbilityId = "flare" });
        fx.Ticks(1);
        Assert.Contains(client.Texts, t => t.Contains("bile at +30 or higher"));

        alice.Bile = 40;
        Npc dummy = fx.Npc("dummy");
        int before = dummy.Blood;
        client.FromClient(new UseAbilityMessage { AbilityId = "flare" });
        fx.Ticks(4);
        Assert.Equal(45, alice.Phlegm);
        Assert.Equal(55, alice.Bile);
        // 200% of 100 ability damage, plus infused for bile 40.
        int bonus = fx.World.Rules.InfusedPercent(40);
        Assert.True(before - dummy.Blood >= 200 * (100 + bonus) / 100);
    }

    [Fact]
    public void BileIsClampedAtTheLimit()
    {
        var fx = Arena(At("dummy", 8, 5));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        alice.Bile = 95;
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(1);
        Assert.Equal(100, alice.Bile);
    }

    [Fact]
    public void StillTheHumoursPullsBileTowardsZeroFromEitherSide()
    {
        var fx = Arena(At("dummy", 8, 5));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });

        alice.Bile = -40;
        client.FromClient(new UseAbilityMessage { AbilityId = "still" });
        fx.Ticks(1);
        Assert.Equal(-15, alice.Bile);

        alice.Bile = 10;
        alice.Cooldowns.Clear();
        alice.GlobalCooldownReadyTick = 0;
        client.FromClient(new UseAbilityMessage { AbilityId = "still" });
        fx.Ticks(1);
        Assert.Equal(0, alice.Bile); // doesn't overshoot past balance
    }

    [Fact]
    public void InfusedBileIncreasesDamageDealt()
    {
        var fx = Arena(At("dummy", 8, 5));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Npc dummy = fx.Npc("dummy");
        alice.Bile = 90; // spark pushes it to 100 *after* the hit, so the bonus is for 90
        client.FromClient(new AttackMessage { TargetId = dummy.Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(1);

        int expected = (int)Math.Round(100 * (1 + fx.World.Rules.InfusedPercent(90) / 100.0));
        Assert.Equal(dummy.MaxBlood - expected, dummy.Blood);
    }

    [Fact]
    public void DraughtHealsPartOfTheDamageDealt()
    {
        var fx = Arena(At("dummy", 8, 5));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        alice.Blood = 500;
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "draught" });
        fx.Ticks(1);
        Assert.Equal(550, alice.Blood);
        Assert.Contains(client.Updates.SelectMany(u => u.Hits), h => h.Kind == HitKind.Heal && h.Amount == 50);
    }

    [Fact]
    public void WardsDeflectionAndBileChangeDamageTaken()
    {
        var fx = Arena(At("rat", 30, 30));
        fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Npc rat = fx.Npc("rat");
        CombatRules rules = fx.World.Rules;

        Assert.Equal(100, Take(100));

        CombatSystem combat = fx.World.Get<CombatSystem>();
        combat.ApplyEffect(alice, EffectType.Ward, 30, 1000);
        Assert.Equal(70, Take(100));
        alice.Effects.Clear();
        fx.World.RecalculateStats(alice);

        ReligionSystem religion = fx.World.Get<ReligionSystem>();
        religion.SetHymn(alice, "deflect_melee", true);
        Assert.Equal(50, Take(100));
        religion.SetHymn(alice, "deflect_ranged", true); // wrong style: no help, and it replaces the melee one
        Assert.Equal(new[] { "deflect_ranged" }, religion.Singing(alice));
        Assert.Equal(100, Take(100));
        religion.Silence(alice);

        alice.Bile = -100; // warding
        Assert.Equal(100 - rules.WardingMaxPercent, Take(100));
        alice.Bile = 100; // choleric
        Assert.Equal(100 + rules.CholericMaxPercent, Take(100));
        alice.Bile = 0;

        int Take(int amount)
        {
            alice.Blood = alice.MaxBlood;
            return fx.World.Get<CombatSystem>().Damage(alice, amount, rat, HitKind.Normal, CombatStyle.Melee);
        }
    }

    [Fact]
    public void ShroudGrantsAWardThatExpires()
    {
        var fx = Arena(At("dummy", 8, 5));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        alice.Phlegm = 50;
        alice.Bile = -40;
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "shroud" });
        fx.Ticks(1);
        Assert.Equal(30, alice.EffectTotal(EffectType.Ward));
        fx.Ticks(10);
        Assert.Equal(0, alice.EffectTotal(EffectType.Ward));
    }

    [Fact]
    public void YellowBileAtTheLimitCostsNoBlood()
    {
        var fx = Arena();
        fx.Login("Alice");
        Player alice = fx.Player("Alice");
        alice.Bile = 100;
        alice.Blood = 30;
        for (int i = 0; i < 20; i++)
        {
            alice.LastCombatTick = fx.World.CurrentTick; // in combat: no regeneration either
            fx.Ticks(1);
        }
        Assert.Equal(30, alice.Blood);
    }

    [Fact]
    public void OutOfCombatBileSettlesPhlegmFadesAndBloodReturns()
    {
        var fx = Arena();
        fx.Login("Alice");
        Player alice = fx.Player("Alice");
        alice.Bile = -20;
        alice.Phlegm = 30;
        alice.Blood = 900;
        fx.Ticks(10);
        Assert.Equal(-10, alice.Bile);
        Assert.Equal(10, alice.Phlegm);
        Assert.Equal(950, alice.Blood);
        fx.Ticks(20);
        Assert.Equal((0, 0, 1000), (alice.Bile, alice.Phlegm, alice.Blood));
    }

    [Fact]
    public void HymnsDrainDevotionAndFallSilentWhenItRunsOut()
    {
        var fx = Arena();
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Assert.Equal(100, alice.Devotion); // a new character starts full
        alice.Devotion = 5;
        client.FromClient(new SetHymnMessage { HymnId = "deflect_melee", On = true });
        fx.Ticks(5);
        Assert.Equal(0, alice.Devotion);
        Assert.Empty(fx.World.Get<ReligionSystem>().Singing(alice));
        Assert.Contains(client.Texts, t => t.Contains("devotion is spent"));
        Assert.Equal(0, alice.Stats.Get(StatIds.DeflectMelee));
        fx.Ticks(20);
        Assert.Equal(0, alice.Devotion); // devotion never comes back by itself
    }

    [Fact]
    public void KillingAnNpcRemovesItThenItRespawnsAtHome()
    {
        var fx = Arena(At("rat", 8, 5));
        FakeConnection client = fx.Login("Alice");
        Npc rat = fx.Npc("rat");
        int ratId = rat.Id;
        client.FromClient(new AttackMessage { TargetId = ratId });

        for (int i = 0; i < 60 && !rat.IsDead; i++) fx.Ticks(1);
        Assert.True(rat.IsDead);
        Assert.Contains(client.Updates, u => u.Deaths.Contains(ratId));
        Assert.Contains("You have defeated the rat.", client.Texts);
        Assert.Equal(0, fx.Player("Alice").TargetId);

        fx.Ticks(3);
        Assert.True(rat.Despawned);
        Assert.Contains(client.Updates, u => u.Removed.Contains(ratId));

        fx.Ticks(6);
        Assert.False(rat.Despawned);
        Assert.Equal(rat.MaxBlood, rat.Blood);
        Assert.Equal(new TileCoord(8, 5), rat.Position);
        Assert.Equal(ratId, client.Updates.Last(u => u.Added.Any(a => a.Id == ratId)).Added.Single(a => a.Id == ratId).Id);
    }

    [Fact]
    public void NpcsFightBackWhenAttacked()
    {
        var fx = Arena(At("rat", 9, 5));
        FakeConnection client = fx.Login("Alice");
        client.FromClient(new AttackMessage { TargetId = fx.Npc("rat").Id });
        fx.Ticks(12);
        Player alice = fx.Player("Alice");
        Assert.True(alice.Blood < alice.MaxBlood);
        Assert.Equal(alice.Id, fx.Npc("rat").TargetId);
    }

    [Fact]
    public void MeleeNpcsAttackOnlyFromOrthogonallyAdjacentTiles()
    {
        var fx = Arena(At("rat", 6, 6)); // diagonal to Alice at (5,5)
        fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Npc rat = fx.Npc("rat");
        rat.TargetId = alice.Id;

        Assert.False(fx.World.Get<CombatSystem>().InRange(rat, alice, 1));
        fx.Ticks(1);
        Assert.True(fx.World.Get<CombatSystem>().InRange(rat, alice, 1));
        Assert.True(Math.Abs(rat.Position.X - 5) + Math.Abs(rat.Position.Y - 5) == 1);
    }

    [Fact]
    public void AWallBetweenThemStopsMelee()
    {
        MapData map = Maps.Open(20, 20, Spawn);
        map.AddWall(0, 5, 5, WallSide.East);
        var fx = new WorldFixture(map, spawns: new[] { At("rat", 6, 5) });
        fx.Login("Alice");
        Assert.False(fx.World.Get<CombatSystem>().InRange(fx.Npc("rat"), fx.Player("Alice"), 1));
    }

    [Fact]
    public void AggressiveNpcsAttackPlayersInRangeAndSight()
    {
        var fx = Arena(At("mite", 8, 5));
        fx.Login("Alice"); // 3 tiles away, aggro radius 4
        fx.Ticks(2);
        Npc mite = fx.Npc("mite");
        Player alice = fx.Player("Alice");
        Assert.Equal(alice.Id, mite.TargetId);
        Assert.True(alice.Blood < alice.MaxBlood);
    }

    [Fact]
    public void AggressiveNpcsDontSeeThroughWalls()
    {
        MapData map = Maps.Open(20, 20, Spawn);
        for (int y = 0; y < 20; y++) map.AddWall(0, 6, y, WallSide.East);
        var fx = new WorldFixture(map, spawns: new[] { At("mite", 8, 5) });
        fx.Login("Alice");
        fx.Ticks(5);
        Assert.Equal(0, fx.Npc("mite").TargetId);
    }

    [Fact]
    public void NpcsLeashBackHomeAndReset()
    {
        var fx = Arena(At("rat", 10, 5));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Npc rat = fx.Npc("rat");
        client.FromClient(new AttackMessage { TargetId = rat.Id });
        fx.Ticks(3);
        Assert.Equal(alice.Id, rat.TargetId);

        // Run far away; the rat follows until its leash (10) snaps.
        client.FromClient(new WalkToMessage { X = 35, Y = 35, Run = true });
        fx.Ticks(40);
        Assert.Equal(0, rat.TargetId);
        Assert.Equal(new TileCoord(10, 5), rat.Position);
        Assert.Equal(rat.MaxBlood, rat.Blood);
    }

    [Fact]
    public void DyingSendsTheFerrymanAndRespawnsBalanced()
    {
        var fx = Arena(At("brute", 6, 5));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        alice.Bile = 50;
        client.FromClient(new AttackMessage { TargetId = fx.Npc("brute").Id });
        for (int i = 0; i < 20 && !alice.IsDead; i++) fx.Ticks(1);

        Assert.True(alice.IsDead);
        Assert.Equal(1, alice.Deaths);
        Assert.Contains(client.Sent.OfType<GameTextMessage>(), t => t.Kind == TextKind.Ferryman && t.Text == "first Alice");

        fx.Ticks(fx.World.Rules.RespawnDelayTicks);
        Assert.False(alice.IsDead);
        Assert.Equal((Spawn, 1000, 0, 0), (alice.Position, alice.Blood, alice.Bile, alice.Phlegm));
        Assert.NotEqual(alice.Id, fx.Npc("brute").TargetId);
    }

    [Fact]
    public void DeathsAndResourcesSurviveLogout()
    {
        var fx = Arena();
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        alice.Deaths = 3;
        alice.Blood = 420;
        alice.Bile = -35;
        alice.Devotion = 42;
        alice.LastCombatTick = fx.World.CurrentTick + 1; // in combat, so nothing regenerates on the way out
        client.FromClient(new LogoutMessage());
        fx.Ticks(1);

        fx.Login("Alice");
        Player again = fx.Player("Alice");
        Assert.Equal((3, -35, 42.0), (again.Deaths, again.Bile, again.Devotion));
        Assert.True(again.Blood >= 420);
    }

    [Fact]
    public void YouCannotAttackOtherPlayers()
    {
        var fx = Arena();
        FakeConnection alice = fx.Login("Alice");
        fx.Login("Bob");
        alice.FromClient(new AttackMessage { TargetId = fx.Player("Bob").Id });
        fx.Ticks(1);
        Assert.Equal(0, fx.Player("Alice").TargetId);
    }

    [Fact]
    public void WalkingAwayKeepsTheTargetUntilOutOfCombat()
    {
        var fx = Arena(At("dummy", 8, 5));
        FakeConnection client = fx.Login("Alice");
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        fx.Ticks(1);
        client.FromClient(new WalkToMessage { X = 2, Y = 2 });
        fx.Ticks(1);
        Assert.Equal(fx.Npc("dummy").Id, fx.Player("Alice").TargetId);
        fx.Ticks(fx.World.Rules.CombatTimeoutTicks);
        Assert.Equal(0, fx.Player("Alice").TargetId);
    }

    [Fact]
    public void BileStatusesAreSentWithTheirStrength()
    {
        var fx = Arena();
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        CombatRules rules = fx.World.Rules;

        alice.Bile = 0;
        Assert.Empty(fx.World.StatusesOf(alice));

        alice.Bile = 50;
        var hot = fx.World.StatusesOf(alice);
        Assert.Equal(new[] { StatusIds.Infused, StatusIds.Choleric }, hot.Select(s => s.Id));
        Assert.Equal(rules.InfusedPercent(50), hot[0].Magnitude);

        alice.Bile = -100; // only a pole's two statuses, even at the limit
        Assert.Equal(new[] { StatusIds.Warding, StatusIds.Melancholic }, fx.World.StatusesOf(alice).Select(s => s.Id));

        fx.World.Get<CombatSystem>().ApplyEffect(alice, EffectType.Ward, 30, 10);
        fx.Ticks(1);
        StatusState ward = client.Sent.OfType<PlayerStateMessage>().Last().Statuses.Single(s => s.Id == "ward");
        Assert.Equal(30, ward.Magnitude);
        Assert.True(ward.EndTick > fx.World.CurrentTick);
    }

    [Fact]
    public void HitsAreOnlySentToPlayersWhoCanSeeThem()
    {
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig { ViewRadius = 10 }, spawns: new[] { At("dummy", 8, 5) });
        FakeConnection near = fx.Login("Near");
        FakeConnection far = fx.Login("Far");
        fx.Player("Far").Position = new TileCoord(35, 35);
        fx.Ticks(1);

        near.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        fx.Ticks(2);
        Assert.Contains(near.Updates, u => u.Hits.Count > 0);
        Assert.DoesNotContain(far.Updates, u => u.Hits.Count > 0);
    }

    [Fact]
    public void ThousandTicksOfFightingKeepsResourcesInBounds()
    {
        var fx = Arena(At("rat", 10, 5), At("rat", 12, 8), At("mite", 15, 5), At("dummy", 20, 20));
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        var random = new Random(3);
        string[] bar = { "spark", "draught", "still", "flare", "shroud", "ult" };
        int kills = 0;

        for (int tick = 0; tick < 1000; tick++)
        {
            if (alice.TargetId == 0 && !alice.IsDead)
            {
                Npc? next = fx.World.Npcs.Where(n => !n.Despawned && !n.IsDead && n.Def.Attacks).OrderBy(n => n.Position.ChebyshevDistance(alice.Position)).FirstOrDefault();
                if (next != null) client.FromClient(new AttackMessage { TargetId = next.Id });
            }
            if (tick % 3 == 0) client.FromClient(new UseAbilityMessage { AbilityId = bar[random.Next(bar.Length)] });
            fx.Ticks(1);
            kills += fx.World.Npcs.Count(n => n.IsDead && n.DiedAtTick == fx.World.CurrentTick);

            Assert.InRange(alice.Phlegm, 0, 100);
            Assert.InRange(alice.Bile, -100, 100);
            Assert.InRange(alice.Blood, 0, alice.MaxBlood);
            Assert.InRange(alice.Devotion, 0, alice.MaxDevotion);
            foreach (Npc n in fx.World.Npcs) Assert.InRange(n.Blood, 0, n.MaxBlood);
        }
        Assert.True(kills >= 5, "Expected a steady stream of kills, got " + kills);
    }

    [Fact]
    public void HotReloadedContentTakesEffectNextTick()
    {
        var fx = Arena(At("dummy", 8, 5));
        FakeConnection client = fx.Login("Alice");
        Npc dummy = fx.Npc("dummy");
        client.FromClient(new AttackMessage { TargetId = dummy.Id });
        fx.Ticks(1);
        int before = dummy.Blood;

        List<AbilityDef> abilities = TestContent.Abilities();
        abilities.Single(a => a.Id == "spark").DamageMin = 1000;
        abilities.Single(a => a.Id == "spark").DamageMax = 1000;
        fx.World.QueueContent(TestContent.Create(abilities: abilities));
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(1);

        Assert.Equal(before - 1000, dummy.Blood);
        Assert.Equal(2, client.Sent.OfType<ContentInfoMessage>().Count()); // login + reload
    }
}
