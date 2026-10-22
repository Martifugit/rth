using Rth.Server.Game;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

public class InspectTests
{
    private static QueryResultMessage Ask(WorldFixture fx, FakeConnection asker, string kind, string arg)
    {
        asker.Sent.Clear();
        asker.FromClient(new QueryMessage { Kind = kind, Arg = arg });
        fx.Ticks(1);
        return asker.Sent.OfType<QueryResultMessage>().Single();
    }

    [Fact]
    public void InspectingAPlayerShowsTheirPublicSide()
    {
        var fx = new WorldFixture(Maps.Open(64, 64, new TileCoord(5, 5)));
        FakeConnection alice = fx.Login("Alice");
        fx.Login("Bob");
        Player bob = fx.Player("Bob");
        fx.World.Get<ItemSystem>().Of(bob).Equipment.Set((int)EquipSlot.Head, "hat", 1);
        fx.World.Get<ItemSystem>().Give(bob, "coins", 500);
        fx.World.Get<SkillSystem>().AddXp(bob, "occultism", 2000);
        fx.World.Get<SkillSystem>().AddXp(bob, "religion", 200);

        QueryResultMessage result = Ask(fx, alice, QueryKinds.Inspect, bob.Id.ToString());

        Assert.True(result.Answered, result.Problem);
        Assert.Equal((QueryKinds.Inspect, bob.Id.ToString()), (result.Kind, result.Arg));
        PlayerInspect inspect = result.Parse<PlayerInspect>()!;
        Assert.Equal(("Bob", bob.Id), (inspect.Name, inspect.EntityId));
        Assert.Equal("hat", inspect.Equipment[(int)EquipSlot.Head]);
        Assert.Equal("", inspect.Equipment[(int)EquipSlot.Weapon]);
        int occultism = fx.World.Get<SkillSystem>().Level(bob, "occultism");
        int religion = fx.World.Get<SkillSystem>().Level(bob, "religion");
        Assert.True(occultism > 1 && religion > 1);
        Assert.Equal(occultism + religion + 3, inspect.TotalLevel); // the other three skills are level 1
        Assert.Equal(new[] { "Occultism", "Religion" }, inspect.Highlights.Take(2).Select(h => h.Label));
        Assert.Equal(occultism.ToString(), inspect.Highlights[0].Value);

        // Public information only: nothing from the pack, no stats or blood.
        Assert.DoesNotContain("coins", result.Json);
        Assert.DoesNotContain("blood", result.Json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ward", result.Json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void YouCanInspectYourself()
    {
        var fx = new WorldFixture(Maps.Open(32, 32, new TileCoord(5, 5)));
        FakeConnection alice = fx.Login("Alice");
        Player me = fx.Player("Alice");

        PlayerInspect inspect = Ask(fx, alice, QueryKinds.Inspect, me.Id.ToString()).Parse<PlayerInspect>()!;

        Assert.Equal("Alice", inspect.Name);
        Assert.Equal(1, inspect.CombatLevel);
        Assert.Equal(5, inspect.TotalLevel);
        Assert.Equal(Enum.GetValues(typeof(EquipSlot)).Length, inspect.Equipment.Count);
        Assert.Empty(inspect.Highlights.Where(h => h.Icon != "quests" && h.Icon != "achievement")); // no skill above 1 yet
    }

    [Fact]
    public void OnlyPlayersYouCanSeeCanBeInspected()
    {
        var fx = new WorldFixture(Maps.Open(64, 64, new TileCoord(5, 5)));
        FakeConnection alice = fx.Login("Alice");
        FakeConnection carol = fx.Login("Carol");
        carol.FromClient(new WalkToMessage { X = 50, Y = 50, Run = true });
        fx.Ticks(30);

        Assert.Equal("They are too far away.", Ask(fx, alice, QueryKinds.Inspect, fx.Player("Carol").Id.ToString()).Problem);
        Assert.Equal("They are no longer here.", Ask(fx, alice, QueryKinds.Inspect, "999999").Problem);
        Assert.Equal("They are no longer here.", Ask(fx, alice, QueryKinds.Inspect, "not a number").Problem);
        QueryResultMessage unknown = Ask(fx, alice, "secrets", "1");
        Assert.False(unknown.Answered);
        Assert.Equal("", unknown.Json);
    }

    [Fact]
    public void SystemsCanAddHighlightsAndQueries()
    {
        var fx = new WorldFixture(Maps.Open(32, 32, new TileCoord(5, 5)));
        FakeConnection alice = fx.Login("Alice");
        Player me = fx.Player("Alice");
        fx.World.Get<InspectSystem>().AddHighlights(p => new[] { new InspectLine { Label = "Deaths", Value = p.Deaths.ToString() } });
        fx.World.Get<QuerySystem>().Register("echo", (asker, arg) => QueryAnswer.Of(new { asker.Name, arg }));

        Assert.Contains(Ask(fx, alice, QueryKinds.Inspect, me.Id.ToString()).Parse<PlayerInspect>()!.Highlights, h => h.Label == "Deaths" && h.Value == "0");
        Assert.Equal("{\"Name\":\"Alice\",\"arg\":\"x\"}", Ask(fx, alice, "echo", "x").Json);
    }
}
