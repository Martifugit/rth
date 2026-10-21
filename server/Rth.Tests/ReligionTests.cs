using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Server.Persistence;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

public class ReligionTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static WorldFixture Arena(params NpcSpawn[] spawns) =>
        new(Maps.Open(40, 40, Spawn), new ServerConfig(), null, spawns);

    private static (WorldFixture Fx, FakeConnection Client, Player Alice, ReligionSystem Religion) Alice(params NpcSpawn[] spawns)
    {
        WorldFixture fx = Arena(spawns);
        FakeConnection client = fx.Login("Alice");
        return (fx, client, fx.Player("Alice"), fx.World.Get<ReligionSystem>());
    }

    [Fact]
    public void AHigherHymnReplacesTheLowerOneInItsLine()
    {
        var (_, _, alice, religion) = Alice();
        religion.SetHymn(alice, "guard", true);
        Assert.Equal(10, alice.Stats.Get(StatIds.Ward));
        religion.SetHymn(alice, "guard2", true);
        Assert.Equal(new[] { "guard2" }, religion.Singing(alice));
        Assert.Equal(20, alice.Stats.Get(StatIds.Ward));
    }

    [Fact]
    public void OnlyOneOverheadAtATimeButOtherHymnsStack()
    {
        var (_, _, alice, religion) = Alice();
        religion.SetHymn(alice, "guard", true);
        religion.SetHymn(alice, "occult_boost", true);
        religion.SetHymn(alice, "deflect_melee", true);
        religion.SetHymn(alice, "leech", true);
        Assert.Equal(new[] { "guard", "occult_boost", "leech" }, religion.Singing(alice));
        Assert.Equal(3, religion.DrainPerTick(alice));
    }

    [Fact]
    public void AHymnNeedsItsReligionLevel()
    {
        var (fx, client, alice, religion) = Alice();
        client.FromClient(new SetHymnMessage { HymnId = "hard", On = true });
        fx.Ticks(1);
        Assert.Empty(religion.Singing(alice));
        Assert.Contains(client.Texts, t => t.Contains("religion level 50"));

        fx.World.Get<SkillSystem>().AddXp(alice, "religion", fx.World.Content.XpCurve.XpForLevel(50));
        client.FromClient(new SetHymnMessage { HymnId = "hard", On = true });
        fx.Ticks(1);
        Assert.Equal(new[] { "hard" }, religion.Singing(alice));
    }

    [Fact]
    public void ReligionLevelsRaiseMaxDevotion()
    {
        var (fx, _, alice, _) = Alice();
        Assert.Equal(100, alice.MaxDevotion); // the test rules' base
        fx.World.Get<SkillSystem>().AddXp(alice, "religion", fx.World.Content.XpCurve.XpForLevel(11));
        Assert.Equal(200, alice.MaxDevotion); // 10 a level above 1
    }

    [Fact]
    public void SingingTrainsReligionOnlyInCombat()
    {
        var (fx, _, alice, religion) = Alice();
        SkillSystem skills = fx.World.Get<SkillSystem>();
        religion.SetHymn(alice, "guard", true);
        fx.Ticks(5);
        Assert.Equal(0, skills.Xp(alice, "religion"));
        Assert.Equal(95, alice.Devotion);

        for (int i = 0; i < 5; i++)
        {
            alice.LastCombatTick = fx.World.CurrentTick + 1;
            fx.Ticks(1);
        }
        Assert.Equal(5, skills.Xp(alice, "religion")); // 1 XP a point in the test content
    }

    [Fact]
    public void StyleBoostsAndLifeStealWorkThroughStats()
    {
        var (fx, client, alice, religion) = Alice(new NpcSpawn { Npc = "dummy", X = 8, Y = 5 });
        Npc dummy = fx.Npc("dummy");
        religion.SetHymn(alice, "occult_boost", true);
        religion.SetHymn(alice, "leech", true);
        alice.Blood = 500;

        client.FromClient(new AttackMessage { TargetId = dummy.Id });
        fx.Ticks(1);
        Assert.Equal(dummy.MaxBlood - 75, dummy.Blood); // the occult auto-attack's 50, +50%
        Assert.True(alice.Blood >= 500 + 37, "Life steal should return half the damage, got " + alice.Blood);
    }

    [Fact]
    public void PrayingAtAnAltarRestoresDevotion()
    {
        var altar = new PlacedObject { Type = "altar", Plane = 0, X = 9, Y = 5 };
        GameContent content = WithAltar();
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig(), content, objects: new[] { altar });
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        alice.Devotion = 3;

        client.FromClient(new ObjectActionMessage { ObjectType = "altar", X = 9, Y = 5, ActionId = ReligionSystem.PrayAction });
        fx.Ticks(6);
        Assert.Equal(alice.MaxDevotion, alice.Devotion);
        Assert.Contains(client.Texts, t => t.Contains("devotion is restored"));
    }

    [Fact]
    public void DyingSilencesHymnsAndRestoresDevotion()
    {
        var (fx, _, alice, religion) = Alice();
        religion.SetHymn(alice, "guard", true);
        alice.Devotion = 10;
        fx.World.Get<CombatSystem>().Damage(alice, alice.Blood * 10, null, HitKind.Normal); // through the guard's ward
        Assert.True(alice.IsDead);
        Assert.Empty(religion.Singing(alice));
        Assert.Equal(alice.MaxDevotion, alice.Devotion);
    }

    [Fact]
    public void DevotionIsSavedButHymnsStopOnLogout()
    {
        var (fx, client, alice, religion) = Alice();
        religion.SetHymn(alice, "guard", true);
        alice.Devotion = 33.5;
        client.FromClient(new LogoutMessage());
        fx.Ticks(1);

        fx.Login("Alice");
        Player again = fx.Player("Alice");
        Assert.True(again.Devotion <= 33.5 && again.Devotion >= 32.5); // at most one tick's drain on the way out
        Assert.Empty(fx.World.Get<ReligionSystem>().Singing(again));
    }

    private static GameContent WithAltar()
    {
        ContentParts parts = TestContent.Parts();
        parts.Objects.Add(new ObjectDef { Id = "altar", Name = "Altar", Actions = { new ActionDef { Id = ReligionSystem.PrayAction, Label = "Pray" } } });
        return GameContent.Create(parts);
    }
}
