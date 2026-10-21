using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Net;
using Rth.Shared.Progress;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>Boss runs: timing, completions and best times, rewards and their drop log, losing a run.</summary>
public class BossTests
{
    private static (WorldFixture Fx, FakeConnection Client, Player Alice, BossSystem Bosses) World()
    {
        ContentParts parts = TestContent.Parts();
        parts.Bosses.Add(new BossDef { Id = "saint", Name = "The Saint", Rewards = "gems" });
        var fx = new WorldFixture(Maps.Open(40, 40, new TileCoord(5, 5)), new ServerConfig(), GameContent.Create(parts));
        FakeConnection client = fx.Login("Alice");
        return (fx, client, fx.Player("Alice"), fx.World.Get<BossSystem>());
    }

    [Fact]
    public void AWinIsTimedCountedAndRewarded()
    {
        var (fx, client, alice, bosses) = World();
        Assert.True(bosses.StartRun(alice, "saint"));
        fx.Ticks(1);
        Assert.Equal("saint", client.Sent.OfType<JsonStateMessage>().Last(m => m.Key == BossSystem.StateKey).Parse<BossRunState>().Boss);
        fx.Ticks(99);
        bosses.EndRun(alice, won: true);

        BossRecord record = fx.World.Get<BestiarySystem>().Boss(alice, "saint");
        Assert.Equal((1, 1, 100), (record.Completions, record.Attempts, record.BestTicks));
        Assert.Equal(1, fx.World.Get<ItemSystem>().CountOf(alice, "gem"));
        Assert.Equal(1, record.Obtained["gem"]);
        Assert.Contains(client.Texts, t => t == "Your completion count for The Saint is: 1.");
        Assert.Contains(client.Texts, t => t == "Duration: 1:00.0 (new personal best).");
        fx.Ticks(1);
        Assert.Equal("", client.Sent.OfType<JsonStateMessage>().Last(m => m.Key == BossSystem.StateKey).Parse<BossRunState>().Boss);
    }

    [Fact]
    public void OnlyAFasterWinIsANewBest()
    {
        var (fx, client, alice, bosses) = World();
        bosses.StartRun(alice, "saint");
        fx.Ticks(50);
        bosses.EndRun(alice, won: true);
        bosses.StartRun(alice, "saint");
        fx.Ticks(80);
        bosses.EndRun(alice, won: true);
        BossRecord record = fx.World.Get<BestiarySystem>().Boss(alice, "saint");
        Assert.Equal((2, 50, 80), (record.Completions, record.BestTicks, record.LastTicks));
        Assert.Contains(client.Texts, t => t == "Duration: 0:48.0. Personal best: 0:30.0.");
    }

    [Fact]
    public void DyingOrLeavingLosesTheRun()
    {
        var (fx, client, alice, bosses) = World();
        bosses.StartRun(alice, "saint");
        fx.World.Get<CombatSystem>().Damage(alice, alice.Blood, null, HitKind.Normal);
        Assert.False(bosses.InRun(alice));
        BossRecord record = fx.World.Get<BestiarySystem>().Boss(alice, "saint");
        Assert.Equal((0, 1), (record.Completions, record.Attempts));
        Assert.Equal(0, fx.World.Get<ItemSystem>().CountOf(alice, "gem"));

        fx.Ticks(10); // respawn
        bosses.StartRun(alice, "saint");
        client.FromClient(new LogoutMessage());
        fx.Ticks(1);
        Assert.Equal(2, fx.Store.Saves["alice"].Section<BestiaryState>("bestiary")!.Bosses["saint"].Attempts);
    }

    [Fact]
    public void BossesAreValidated()
    {
        ContentParts parts = TestContent.Parts();
        parts.Bosses.Add(new BossDef { Id = "bad", Rewards = "missing" });
        var e = Assert.Throws<ContentException>(() => GameContent.Create(parts));
        Assert.Contains("needs a name", e.Message);
        Assert.Contains("'missing'", e.Message);
    }
}
