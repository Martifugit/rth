using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>
/// The skilling loops played end to end on the shipped content, from a new character's starter
/// kit: catch and cook, mine, smelt and smith, pick, clean and brew. Catches the gaps between
/// pieces (a missing tool, a use, an action) that unit tests of each mechanism can't.
/// </summary>
public class SkillingLoopTests
{
    private static readonly TileCoord Spawn = new(10, 10);

    private static readonly PlacedObject[] Placed =
    {
        new() { Type = "river_spot", X = 12, Y = 10 },
        new() { Type = "copper_seam", X = 8, Y = 10 },
        new() { Type = "furnace", X = 10, Y = 13 },
        new() { Type = "forge", X = 13, Y = 13 },
        new() { Type = "range", X = 7, Y = 13 },
        new() { Type = "yarrow_patch", X = 10, Y = 7 },
        new() { Type = "trough", X = 13, Y = 7 },
        new() { Type = "apothecary_bench", X = 7, Y = 7 },
        new() { Type = "alder_tree", X = 4, Y = 10 },
    };

    private static (WorldFixture Fx, FakeConnection Alice, Player P) World()
    {
        var fx = new WorldFixture(Maps.Open(24, 24, Spawn), new ServerConfig(), GameContent.Load(Repo.ContentDirectory), null, Placed);
        FakeConnection alice = fx.Login("Alice");
        return (fx, alice, fx.Player("Alice"));
    }

    private static PlacedObject O(string type) => Placed.First(p => p.Type == type);

    private static void Act(WorldFixture fx, FakeConnection c, string type, string action, int ticks)
    {
        PlacedObject o = O(type);
        c.FromClient(new ObjectActionMessage { ObjectType = type, ActionId = action, Plane = 0, X = o.X, Y = o.Y });
        fx.Ticks(ticks);
    }

    private static int Slot(WorldFixture fx, Player p, string item)
    {
        Rth.Server.Items.ItemContainer pack = fx.World.Get<ItemSystem>().Of(p).Inventory;
        return Enumerable.Range(0, pack.Size).First(i => !pack.IsEmpty(i) && pack[i].ItemId == item);
    }

    private static int Count(WorldFixture fx, Player p, string item) => fx.World.Get<ItemSystem>().CountOf(p, item);

    private static void MakeFromWindow(WorldFixture fx, FakeConnection c, string recipe, int count, int ticks)
    {
        c.FromClient(new IntentMessage { Kind = MakeState.Intent, Arg = recipe + "|" + count });
        fx.Ticks(ticks);
    }

    private static void Drop(WorldFixture fx, Player p, string item)
    {
        while (Count(fx, p, item) > 0) fx.World.Get<ItemSystem>().Take(p, item, 1);
    }

    [Fact]
    public void CatchLightAFireAndCook()
    {
        var (fx, alice, p) = World();
        Assert.Equal(1, Count(fx, p, "fishing_rod"));
        Assert.Equal(1, Count(fx, p, "tinderbox"));
        fx.World.Get<ItemSystem>().Give(p, "alder_log", 1);
        // A player at the pond: fish while the spot is there, cast again when it comes back.
        GatheringSystem.Node spot = fx.World.Get<GatheringSystem>().Nodes.Single(n => n.Placed.Type == "river_spot");
        for (int t = 0; t < 3000 && Count(fx, p, "raw_perch") < 3; t += 5)
        {
            if (p.Interaction == null && (!spot.Dormant || spot.IsBubbling(fx.World.CurrentTick))) Act(fx, alice, "river_spot", "gather", 5);
            else fx.Ticks(5);
        }
        int perch = Count(fx, p, "raw_perch");
        Assert.True(perch >= 3, "caught " + perch);
        Assert.True(fx.World.Get<SkillSystem>().Xp(p, "fishing") > 0);

        // A campfire from alder logs, then cook the perch on it.
        alice.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = Slot(fx, p, "alder_log"), ActionId = ItemActions.Place });
        fx.Ticks(1);
        PlacedObject? fire = fx.World.Get<ObjectSystem>().AllAt(0, p.Position).FirstOrDefault(o => o.Type == "campfire");
        Assert.NotNull(fire);
        alice.FromClient(new ObjectActionMessage { ObjectType = "campfire", ActionId = "process", Plane = 0, X = fire!.X, Y = fire.Y });
        fx.Ticks(perch * 4 + 6);
        Assert.Equal(perch, Count(fx, p, "perch") + Count(fx, p, "burnt_fish"));
        Assert.Equal(0, Count(fx, p, "raw_perch"));
    }

    [Fact]
    public void MineSmeltAndSmith()
    {
        var (fx, alice, p) = World();
        Drop(fx, p, "riverbread");                                // room in the pack
        Act(fx, alice, "copper_seam", "gather", 300);
        int ore = Count(fx, p, "raw_copper");
        Assert.True(ore >= 3, "mined " + ore);

        Act(fx, alice, "furnace", "process", ore * 4 + 10);
        Assert.Equal(ore, Count(fx, p, "copper_bar"));

        // Several things to make from bars: the make window opens.
        Act(fx, alice, "forge", "process", 3);
        MakeState window = alice.Sent.OfType<JsonStateMessage>().Last(m => m.Key == MakeState.StateKey).Parse<MakeState>();
        Assert.True(window.IsOpen);
        Assert.Contains(window.Options, o => o.Recipe == "smith_copper_dagger" && o.Makeable == ore);
        MakeFromWindow(fx, alice, "smith_copper_dagger", 2, 20);
        Assert.Equal(2, Count(fx, p, "copper_dagger"));
        Assert.True(fx.World.Get<SkillSystem>().Xp(p, "smithing") > 0);

        // The copper dagger got its stats from its gear family.
        EquipmentDef dagger = fx.World.Content.Items["copper_dagger"].Equipment!;
        Assert.True(dagger.Stats["abilityDamage"] > 0);
    }

    [Fact]
    public void PickCleanFillAndBrew()
    {
        var (fx, alice, p) = World();
        Drop(fx, p, "riverbread");
        Act(fx, alice, "yarrow_patch", "gather", 60);
        int grimy = Count(fx, p, "grimy_yarrow");
        Assert.True(grimy >= 2, "picked " + grimy);

        alice.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = Slot(fx, p, "grimy_yarrow"), ActionId = ItemActions.Process });
        fx.Ticks(grimy * 2 + 4);
        Assert.Equal(grimy, Count(fx, p, "yarrow"));

        fx.World.Get<ItemSystem>().Give(p, "empty_vial", 1);
        alice.FromClient(new UseItemMessage { Slot = Slot(fx, p, "empty_vial"), ItemId = "empty_vial", Target = UseTarget.Object, ObjectType = "trough", Plane = 0, X = O("trough").X, Y = O("trough").Y });
        fx.Ticks(6);
        Assert.True(Count(fx, p, "vial_of_water") == 1, string.Join(" | ", alice.Texts.TakeLast(5)));

        Act(fx, alice, "apothecary_bench", "process", 8);
        Assert.Equal(1, Count(fx, p, "yarrow_potion_unf"));
        fx.World.Get<ItemSystem>().Give(p, "perch_scale", 1);
        Act(fx, alice, "apothecary_bench", "process", 8);
        Assert.Equal(1, Count(fx, p, "bracing_tonic_4"));

        // Drinking leaves the next dose.
        alice.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = Slot(fx, p, "bracing_tonic_4"), ActionId = ItemActions.Consume });
        fx.Ticks(1);
        Assert.Equal(1, Count(fx, p, "bracing_tonic_3"));
    }

    [Fact]
    public void TheAlderNeedsTendingNowAndThen()
    {
        var (fx, alice, p) = World();
        GatheringSystem gathering = fx.World.Get<GatheringSystem>();
        GatheringSystem.Node alder = gathering.Nodes.Single(n => n.Placed.Type == "alder_tree");
        Act(fx, alice, "alder_tree", "gather", 1);                 // a tree only wants tending while someone chops by it
        for (int i = 0; i < 3000 && !alder.NeedsTending(fx.World.CurrentTick); i++)
        {
            if (p.Interaction == null) Act(fx, alice, "alder_tree", "gather", 1);   // back to it if it was cut down
            fx.Ticks(1);
        }
        Assert.True(alder.NeedsTending(fx.World.CurrentTick));
        Act(fx, alice, "alder_tree", "tend", 10);
        Assert.True(alder.IsTended(fx.World.CurrentTick));
        Assert.True(fx.World.Get<SkillSystem>().Xp(p, "woodcutting") > 0);
    }
}
