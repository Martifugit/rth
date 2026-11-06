using System.Linq;
using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>
/// How moving and fighting feel: the run toggle taking effect mid-walk, interactions replanning
/// the walk, "I can't reach that!", the logout timer, auto-retaliate and the server saying why a
/// press was ignored.
/// </summary>
public class FeelTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static WorldFixture Open(params NpcSpawn[] spawns) =>
        new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig(), null, spawns);

    [Fact]
    public void RunToggleTakesEffectMidWalk()
    {
        WorldFixture fx = Open();
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");

        client.FromClient(new WalkToMessage { X = 5, Y = 30 });
        fx.Ticks(1);
        Assert.Equal(new TileCoord(5, 6), alice.Position); // walking: one tile a tick

        client.FromClient(new SetClientStateMessage { Key = ClientStateSystem.RunKey, Json = "true" });
        fx.Ticks(1);
        Assert.Equal(new TileCoord(5, 8), alice.Position); // R pressed mid-walk: running from the next step

        client.FromClient(new SetClientStateMessage { Key = ClientStateSystem.RunKey, Json = "false" });
        fx.Ticks(1);
        Assert.Equal(new TileCoord(5, 9), alice.Position);
    }

    [Fact]
    public void TakingAnItemMidWalkHeadsForTheItemAtOnce()
    {
        WorldFixture fx = Open();
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        client.FromClient(new WalkToMessage { X = 5, Y = 30 });
        fx.Ticks(1);
        Assert.Equal(new TileCoord(5, 6), alice.Position);

        GroundItem gem = fx.World.Get<GroundItemSystem>().Spawn("gem", 1, 0, new TileCoord(9, 5), null);
        client.FromClient(new PickupMessage { GroundItemId = gem.Id });
        fx.Ticks(1);
        // Not a step further north: the walk is over and the item is the way now.
        Assert.True(alice.Position.Y <= 6, "kept walking north to " + alice.Position);
        Assert.True(alice.Position.ChebyshevDistance(new TileCoord(9, 5)) < 4, "didn't head for the item: " + alice.Position);
        fx.Ticks(6);
        Assert.Equal(1, fx.World.Get<ItemSystem>().CountOf(alice, "gem"));
    }

    [Fact]
    public void AnUnreachableTargetSaysSoAndStopsTheChase()
    {
        // The rat sits in a walled cell at (5, 5); Alice spawns at (1, 1) outside it.
        MapData map = Maps.FromText(
            "..........",
            "..........",
            "..........",
            "....###...",
            "....#.#...",
            "....###...",
            "..........",
            "..........",
            ".S........",
            "..........");
        var fx = new WorldFixture(map, new ServerConfig(), null, new[] { new NpcSpawn { Npc = "rat", X = 5, Y = 5 } });
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Npc rat = fx.Npc("rat");

        // Melee needs a tile beside the rat; there is none to be had.
        client.FromClient(new AttackMessage { TargetId = rat.Id });
        fx.Ticks(1);
        client.FromClient(new UseAbilityMessage { AbilityId = "poke" }); // range 1
        fx.Ticks(2);
        Assert.Contains(client.Texts, t => t.Contains("can't reach"));
        Assert.Null(alice.Interaction);
        Assert.Equal(rat.Id, alice.TargetId); // kept: an ability can still take the fight up from elsewhere
    }

    [Fact]
    public void LoggingOutIsRefusedUntilAWhileAfterCombat()
    {
        WorldFixture fx = Open(new NpcSpawn { Npc = "dummy", X = 7, Y = 5 });
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        fx.Ticks(2);
        Assert.True(alice.InCombat(fx.World.CurrentTick, fx.World.Rules.LogoutCombatTicks));

        client.FromClient(new LogoutMessage());
        fx.Ticks(1);
        Assert.NotNull(fx.World.FindPlayer("Alice"));
        Assert.Contains(client.Texts, t => t.Contains("can't log out"));

        fx.World.StopInteraction(alice);
        fx.Ticks(fx.World.Rules.LogoutCombatTicks + 1);
        client.FromClient(new LogoutMessage());
        fx.Ticks(1);
        Assert.Null(fx.World.FindPlayer("Alice"));
    }

    [Fact]
    public void ADroppedConnectionStaysInTheWorldWhileFightingThenSaves()
    {
        WorldFixture fx = Open(new NpcSpawn { Npc = "dummy", X = 7, Y = 5 });
        FakeConnection client = fx.Login("Alice");
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        fx.Ticks(2);

        client.Drop();
        fx.Ticks(1);
        Assert.NotNull(fx.World.FindPlayer("Alice")); // still there, still fighting
        fx.Ticks(fx.World.Rules.LogoutCombatTicks);
        Assert.Null(fx.World.FindPlayer("Alice"));
        Assert.Single(fx.Store.Saves);

        // Out of combat, a dropped player leaves at once.
        FakeConnection again = fx.Login("Bob");
        again.Drop();
        fx.Ticks(1);
        Assert.Null(fx.World.FindPlayer("Bob"));
    }

    private sealed class Chopping : Interaction
    {
        public override bool Tick(World world, Player player) => true;
    }

    [Fact]
    public void AutoRetaliateInterruptsSkillingAndCanBeTurnedOff()
    {
        // A mite three tiles off attacks on sight.
        WorldFixture fx = Open(new NpcSpawn { Npc = "mite", X = 8, Y = 5 });
        fx.Login("Alice");
        Player alice = fx.Player("Alice");
        fx.World.StartInteraction(alice, new Chopping());
        fx.Ticks(12);
        Assert.True(alice.Blood < alice.MaxBlood, "the mite never hit");
        Assert.IsType<AttackInteraction>(alice.Interaction); // the chopping stopped: she fights back

        WorldFixture quiet = Open(new NpcSpawn { Npc = "mite", X = 8, Y = 5 });
        FakeConnection bob = quiet.Login("Bob");
        Player b = quiet.Player("Bob");
        bob.FromClient(new SetClientStateMessage { Key = CombatOptions.RetaliateKey, Json = "false" });
        quiet.World.StartInteraction(b, new Chopping());
        quiet.Ticks(12);
        Assert.True(b.Blood < b.MaxBlood, "the mite never hit");
        Assert.IsType<Chopping>(b.Interaction);
        Assert.Equal(0, b.TargetId);
    }

    [Fact]
    public void AnIgnoredPressIsExplained()
    {
        WorldFixture fx = Open(new NpcSpawn { Npc = "dummy", X = 7, Y = 5 });
        FakeConnection client = fx.Login("Alice");
        client.FromClient(new SetClientStateMessage { Key = CombatOptions.QueueingKey, Json = CombatOptions.QueueingValue(false) });
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        fx.Ticks(1);
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(1);
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" }); // on the global cooldown, queueing off
        fx.Ticks(1);
        Assert.Contains("Spark isn't ready yet.", client.Texts);

        Player alice = fx.Player("Alice");
        ItemSystem items = fx.World.Get<ItemSystem>();
        items.Give(alice, "bread", 2);
        int slot = items.Of(alice).Inventory.Snapshot().ToList().FindIndex(s => s.ItemId == "bread");
        alice.Blood = 1;
        client.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = slot, ActionId = ItemActions.Consume });
        fx.Ticks(1);
        slot = items.Of(alice).Inventory.Snapshot().ToList().FindIndex(s => s.ItemId == "bread"); // the second loaf
        client.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = slot, ActionId = ItemActions.Consume });
        fx.Ticks(1);
        Assert.Contains(client.Texts, t => t.Contains("can't eat again"));
    }

    [Fact]
    public void CreaturesGetACombatLevelForTheirMenus()
    {
        WorldFixture fx = Open();
        var rules = new CombatRules();
        Assert.Equal(2, rules.NpcLevel(300, 10));  // (3 + 1) / 2
        Assert.Equal(5, rules.NpcLevel(450, 45));  // 4.5 rounds up
        Assert.Equal(1, rules.NpcLevel(10, 1));
        ClientContent content = fx.World.Content.ToMessage().Parse();
        Assert.Equal(2, content.Npcs.First(n => n.Id == "rat").Level);
    }
}
