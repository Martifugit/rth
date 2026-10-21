using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>Object and NPC actions that are just an animation: kneeling at a shrine or to a king.</summary>
public class ActionAnimationTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static List<string> AnimationsOf(FakeConnection watcher, int entityId) =>
        watcher.Updates.SelectMany(u => u.Actions).Where(a => a.SourceId == entityId).Select(a => a.AbilityId).ToList();

    [Fact]
    public void KneelingAtAShrineWalksThereFirstThenKneelsFacingIt()
    {
        var fx = new WorldFixture(Maps.Open(30, 30, Spawn), objects: new[] { new PlacedObject { Type = "shrine", X = 12, Y = 5 } });
        FakeConnection alice = fx.Login("Alice");
        FakeConnection bob = fx.Login("Bob");
        fx.Ticks(1);
        int aliceId = fx.Player("Alice").Id;

        alice.FromClient(new ObjectActionMessage { ObjectType = "shrine", X = 12, Y = 5, ActionId = "kneel" });
        fx.Ticks(1);
        Assert.DoesNotContain(ActionEvent.AnimationPrefix + "kneel", AnimationsOf(bob, aliceId)); // still walking

        fx.Ticks(10);
        Assert.True(fx.World.IsBeside(fx.Player("Alice"), 0, new TileCoord(12, 5)));
        ActionEvent kneel = bob.Updates.SelectMany(u => u.Actions).Single(a => a.SourceId == aliceId && a.AbilityId == ActionEvent.AnimationPrefix + "kneel");
        Assert.Equal(new TileCoord(12, 5), kneel.Face);
        Assert.Contains("You kneel.", alice.Texts);
    }

    [Fact]
    public void KneelingToAKingNeedsNoHandler()
    {
        var fx = new WorldFixture(Maps.Open(30, 30, Spawn), spawns: new[] { new NpcSpawn { Npc = "king", X = 9, Y = 5 } });
        FakeConnection alice = fx.Login("Alice");
        fx.Ticks(1);

        alice.FromClient(new NpcActionMessage { NpcId = fx.Npc("king").Id, ActionId = "kneel" });
        fx.Ticks(8);

        Assert.Contains(ActionEvent.AnimationPrefix + "kneel", AnimationsOf(alice, fx.Player("Alice").Id));
    }
}
