using Newtonsoft.Json.Linq;
using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Server.Items;
using Rth.Server.Persistence;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

public class ItemTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static WorldFixture Arena(Action<ContentParts>? tweak = null, params NpcSpawn[] spawns)
    {
        ContentParts parts = TestContent.Parts();
        tweak?.Invoke(parts);
        return new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig(), GameContent.Create(parts), spawns);
    }

    private static PlayerItems Items(WorldFixture fx, string name) => fx.World.Get<ItemSystem>().Of(fx.Player(name));

    private static void Give(WorldFixture fx, string name, string item, int count = 1) =>
        Assert.Equal(0, fx.World.Get<ItemSystem>().Give(fx.Player(name), item, count));

    private static int SlotOf(ItemContainer c, string item)
    {
        for (int i = 0; i < c.Size; i++)
            if (c[i].ItemId == item) return i;
        return -1;
    }

    private static void Use(FakeConnection client, ContainerKind where, int slot, string action) =>
        client.FromClient(new ItemActionMessage { Container = where, Slot = slot, ActionId = action });

    // ------------------------------------------------------------------ containers

    [Fact]
    public void StackablesShareASlotAndOthersTakeOneEach()
    {
        var c = new ItemContainer(4);
        var coins = new ItemDef { Id = "coins", Stackable = true };
        var gem = new ItemDef { Id = "gem" };

        Assert.Equal(0, c.Add(coins, 10));
        Assert.Equal(0, c.Add(coins, 5));
        Assert.Equal(1, c.Add(gem, 4)); // three free slots, four gems
        Assert.Equal((15, 3, 0), (c.CountOf("coins"), c.CountOf("gem"), c.FreeSlots));
        Assert.False(c.CanAdd(gem, 1));
        Assert.True(c.CanAdd(coins, 100)); // joins the existing stack

        Assert.Equal(2, c.Remove("gem", 2));
        Assert.Equal(2, c.FreeSlots);
    }

    [Fact]
    public void StacksNeverOverflow()
    {
        var c = new ItemContainer(1);
        var coins = new ItemDef { Id = "coins", Stackable = true };
        c.Add(coins, int.MaxValue - 5);
        Assert.Equal(5, c.Add(coins, 10));
        Assert.Equal(int.MaxValue, c.CountOf("coins"));
    }

    // ------------------------------------------------------------------ starter kit and saving

    [Fact]
    public void NewPlayersGetTheStarterKit()
    {
        var fx = Arena(p =>
        {
            p.World.StarterInventory.Add(new ItemAmount { Item = "bread", Count = 3 });
            p.World.StarterEquipment.Add("sword");
        });
        FakeConnection client = fx.Login("Alice");
        PlayerItems items = Items(fx, "Alice");

        Assert.Equal(3, items.Inventory.CountOf("bread"));
        Assert.Equal("sword", items.Equipment[(int)EquipSlot.Weapon].ItemId);
        Assert.Equal(150, fx.Player("Alice").Stats.Get(StatIds.AbilityDamage));
        Assert.Contains(client.Sent.OfType<ContainerMessage>(), m => m.Kind == ContainerKind.Inventory);
        Assert.Contains(client.Sent.OfType<StatsMessage>(), m => m.Stats[StatIds.AbilityDamage] == 150);
    }

    [Fact]
    public void ItemsSurviveLogoutAndUnknownSaveSectionsArePreserved()
    {
        var fx = Arena();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "Alice", "coins", 42);
        Give(fx, "Alice", "hat");
        Use(client, ContainerKind.Inventory, SlotOf(Items(fx, "Alice").Inventory, "hat"), ItemActions.Equip);
        fx.Ticks(1);
        fx.Store.Saves.Clear();
        client.FromClient(new LogoutMessage());
        fx.Ticks(1);

        PlayerSave save = fx.Store.Saves["alice"];
        save.Sections["future_skill"] = JToken.Parse("{ \"level\": 7 }");
        ((JArray)save.Sections["items"]["Inventory"]!).Add(JToken.Parse("{ \"Slot\": 20, \"Item\": \"removed_item\", \"Count\": 2 }"));

        FakeConnection again = fx.Login("Alice");
        PlayerItems items = Items(fx, "Alice");
        Assert.Equal(42, items.Inventory.CountOf("coins"));
        Assert.Equal("hat", items.Equipment[(int)EquipSlot.Head].ItemId);
        Assert.Equal(1100, fx.Player("Alice").MaxBlood);
        Assert.Equal(2, items.Inventory.CountOf("removed_item")); // content no longer knows it, but it's kept

        again.FromClient(new LogoutMessage());
        fx.Ticks(1);
        Assert.Equal(7, fx.Store.Saves["alice"].Sections["future_skill"]["level"]!.Value<int>());
        Assert.Contains("removed_item", fx.Store.Saves["alice"].Sections["items"]!.ToString());
    }

    // ------------------------------------------------------------------ consumables

    [Fact]
    public void EatingHealsUsesUpTheItemAndHasACooldown()
    {
        var fx = Arena();
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Give(fx, "Alice", "bread", 2);
        alice.Blood = 500;
        alice.LastCombatTick = 1;

        int slot = SlotOf(Items(fx, "Alice").Inventory, "bread");
        Use(client, ContainerKind.Inventory, slot, ItemActions.Consume);
        Use(client, ContainerKind.Inventory, slot + 1, ItemActions.Consume); // same tick: on cooldown
        alice.LastCombatTick = fx.World.CurrentTick + 1; // keep regeneration out of the numbers
        fx.Ticks(1);

        Assert.Equal(600, alice.Blood);
        Assert.Equal(1, Items(fx, "Alice").Inventory.CountOf("bread"));
        Assert.Contains("You eat the bread.", client.Texts);
        Assert.True(alice.ActionDelayUntilTick > fx.World.CurrentTick);
    }

    [Fact]
    public void EatingAndDrinkingAreShownWithTheItemsAnimation()
    {
        var fx = Arena();
        FakeConnection alice = fx.Login("Alice");
        FakeConnection bob = fx.Login("Bob");
        fx.Ticks(1);
        Give(fx, "Alice", "bread");
        Give(fx, "Alice", "tonic");

        Use(alice, ContainerKind.Inventory, SlotOf(Items(fx, "Alice").Inventory, "bread"), ItemActions.Consume);
        Use(alice, ContainerKind.Inventory, SlotOf(Items(fx, "Alice").Inventory, "tonic"), ItemActions.Consume);
        fx.Ticks(1);

        // Bob, standing nearby, sees both: the default for food, the tonic's own for the tonic.
        int aliceId = fx.Player("Alice").Id;
        var shown = bob.Updates.SelectMany(u => u.Actions).Where(a => a.SourceId == aliceId).Select(a => a.AbilityId).ToList();
        Assert.Contains(ActionEvent.AnimationPrefix + "consume", shown);
        Assert.Contains(ActionEvent.AnimationPrefix + "drink", shown);
    }

    [Fact]
    public void PotionsApplyEveryEffectAndLeaveTheirVialInTheSameSlot()
    {
        var fx = Arena();
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Give(fx, "Alice", "gem");
        Give(fx, "Alice", "tonic");
        int slot = SlotOf(Items(fx, "Alice").Inventory, "tonic");
        alice.LastCombatTick = 1;
        alice.LastCombatTick = fx.World.CurrentTick + 1;

        Use(client, ContainerKind.Inventory, slot, ItemActions.Consume);
        fx.Ticks(1);

        Assert.Equal((30, -20), (alice.Phlegm, alice.Bile));
        Assert.Equal("vial", Items(fx, "Alice").Inventory[slot].ItemId);
    }

    [Fact]
    public void EatingDelaysTheNextAttack()
    {
        var fx = Arena(null, new NpcSpawn { Npc = "dummy", X = 8, Y = 5 });
        FakeConnection client = fx.Login("Alice");
        Give(fx, "Alice", "bread");
        Npc dummy = fx.Npc("dummy");

        Use(client, ContainerKind.Inventory, SlotOf(Items(fx, "Alice").Inventory, "bread"), ItemActions.Consume);
        client.FromClient(new AttackMessage { TargetId = dummy.Id });
        fx.Ticks(1);
        Assert.Equal(dummy.MaxBlood, dummy.Blood); // still chewing
        fx.Ticks(2);
        Assert.True(dummy.Blood < dummy.MaxBlood);
    }

    [Fact]
    public void ActionsAnItemDoesntOfferAreRefused()
    {
        var fx = Arena();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "Alice", "bread");
        Use(client, ContainerKind.Inventory, SlotOf(Items(fx, "Alice").Inventory, "bread"), ItemActions.Equip);
        fx.Ticks(1);
        Assert.True(Items(fx, "Alice").Equipment.FreeSlots == Items(fx, "Alice").Equipment.Size);
        Assert.Equal(1, Items(fx, "Alice").Inventory.CountOf("bread"));
    }

    // ------------------------------------------------------------------ equipment and stats

    [Fact]
    public void WieldingSwapsWithWhatWasWornAndChangesStats()
    {
        var fx = Arena();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "Alice", "club");
        Give(fx, "Alice", "sword");
        PlayerItems items = Items(fx, "Alice");

        Use(client, ContainerKind.Inventory, SlotOf(items.Inventory, "club"), ItemActions.Equip);
        fx.Ticks(1);
        Assert.Equal(120, fx.Player("Alice").Stats.Get(StatIds.AbilityDamage));

        int swordSlot = SlotOf(items.Inventory, "sword");
        Use(client, ContainerKind.Inventory, swordSlot, ItemActions.Equip);
        fx.Ticks(1);
        Assert.Equal("sword", items.Equipment[(int)EquipSlot.Weapon].ItemId);
        Assert.Equal("club", items.Inventory[swordSlot].ItemId); // the club took the sword's slot
        Assert.Equal(150, fx.Player("Alice").Stats.Get(StatIds.AbilityDamage));
    }

    [Fact]
    public void EquipmentStatsFeedCombat()
    {
        var fx = Arena(null, new NpcSpawn { Npc = "dummy", X = 8, Y = 5 }, new NpcSpawn { Npc = "rat", X = 30, Y = 30 });
        FakeConnection client = fx.Login("Alice");
        Give(fx, "Alice", "sword");
        Give(fx, "Alice", "hat");
        PlayerItems items = Items(fx, "Alice");
        Use(client, ContainerKind.Inventory, SlotOf(items.Inventory, "sword"), ItemActions.Equip);
        Use(client, ContainerKind.Inventory, SlotOf(items.Inventory, "hat"), ItemActions.Equip);
        fx.Ticks(1);

        Npc dummy = fx.Npc("dummy");
        client.FromClient(new AttackMessage { TargetId = dummy.Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(1);
        Assert.Equal(dummy.MaxBlood - 150, dummy.Blood); // spark is 100% of 150 ability damage

        Player alice = fx.Player("Alice");
        alice.Blood = alice.MaxBlood;
        alice.Bile = 0; // spark's lean would make her choleric
        Assert.Equal(90, fx.World.Get<CombatSystem>().Damage(alice, 100, fx.Npc("rat"), HitKind.Normal)); // hat: 10% ward
    }

    [Fact]
    public void RemovingNeedsInventorySpace()
    {
        var fx = Arena();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "Alice", "hat");
        PlayerItems items = Items(fx, "Alice");
        Use(client, ContainerKind.Inventory, SlotOf(items.Inventory, "hat"), ItemActions.Equip);
        fx.Ticks(1);
        Give(fx, "Alice", "gem", items.Inventory.FreeSlots);

        Use(client, ContainerKind.Equipment, (int)EquipSlot.Head, ItemActions.Unequip);
        fx.Ticks(1);
        Assert.Equal("hat", items.Equipment[(int)EquipSlot.Head].ItemId);
        Assert.Contains("You don't have enough inventory space.", client.Texts);
        Assert.Equal(1100, fx.Player("Alice").MaxBlood);
    }

    [Fact]
    public void HotReloadedItemStatsApplyToWornGear()
    {
        var fx = Arena();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "Alice", "sword");
        Use(client, ContainerKind.Inventory, SlotOf(Items(fx, "Alice").Inventory, "sword"), ItemActions.Equip);
        fx.Ticks(1);

        ContentParts parts = TestContent.Parts();
        parts.Items.Single(i => i.Id == "sword").Equipment!.Stats[StatIds.AbilityDamage] = 75;
        fx.World.QueueContent(GameContent.Create(parts));
        fx.Ticks(1);
        Assert.Equal(175, fx.Player("Alice").Stats.Get(StatIds.AbilityDamage));
    }

    [Fact]
    public void MovingSwapsInventorySlots()
    {
        var fx = Arena();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "Alice", "gem");
        PlayerItems items = Items(fx, "Alice");
        int from = SlotOf(items.Inventory, "gem");
        client.FromClient(new MoveItemMessage { From = from, To = 27 });
        fx.Ticks(1);
        Assert.Equal("gem", items.Inventory[27].ItemId);
        Assert.True(items.Inventory.IsEmpty(from));
    }

    // ------------------------------------------------------------------ ground items and loot

    [Fact]
    public void DroppedItemsAreYoursFirstThenEveryonesThenGone()
    {
        var fx = Arena();
        FakeConnection alice = fx.Login("Alice");
        FakeConnection bob = fx.Login("Bob");
        Give(fx, "Alice", "gem");
        Use(alice, ContainerKind.Inventory, SlotOf(Items(fx, "Alice").Inventory, "gem"), ItemActions.Drop);
        fx.Ticks(1);

        GroundItem gem = fx.World.Get<GroundItemSystem>().Items.Single();
        Assert.Equal(Spawn, gem.Position);
        Assert.Contains(alice.Updates, u => u.GroundAdded.Any(g => g.Id == gem.Id));
        Assert.DoesNotContain(bob.Updates, u => u.GroundAdded.Any(g => g.Id == gem.Id));

        fx.Ticks(10); // private for 10 ticks in the test content
        Assert.Contains(bob.Updates, u => u.GroundAdded.Any(g => g.Id == gem.Id));

        fx.Ticks(20); // gone 30 ticks after it appeared
        Assert.Empty(fx.World.Get<GroundItemSystem>().Items);
        Assert.Contains(alice.Updates, u => u.GroundRemoved.Contains(gem.Id));
        Assert.Contains(bob.Updates, u => u.GroundRemoved.Contains(gem.Id));
    }

    [Fact]
    public void KillsDropLootForTheKillerWhoCanWalkOverAndPickItUp()
    {
        var fx = Arena(null, new NpcSpawn { Npc = "rat", X = 9, Y = 5 });
        FakeConnection client = fx.Login("Alice");
        Give(fx, "Alice", "coins", 3);
        Npc rat = fx.Npc("rat");
        TileCoord where = rat.Position;
        client.FromClient(new AttackMessage { TargetId = rat.Id });
        for (int i = 0; i < 100 && !rat.IsDead; i++) fx.Ticks(1);
        Assert.True(rat.IsDead);
        where = rat.Position;

        GroundItem loot = fx.World.Get<GroundItemSystem>().Items.Single();
        Assert.Equal(("coins", 5, "alice", where), (loot.ItemId, loot.Count, loot.OwnerKey, loot.Position));

        client.FromClient(new PickupMessage { GroundItemId = loot.Id });
        fx.Ticks(10);
        Assert.Equal(8, Items(fx, "Alice").Inventory.CountOf("coins")); // stacked onto the 3 she had
        Assert.Empty(fx.World.Get<GroundItemSystem>().Items);
        Assert.Equal(where, fx.Player("Alice").Position);
    }

    [Fact]
    public void PickingUpWithAFullInventoryLeavesItThere()
    {
        var fx = Arena();
        FakeConnection client = fx.Login("Alice");
        GroundItem gem = fx.World.Get<GroundItemSystem>().Spawn("gem", 1, 0, Spawn, null);
        Give(fx, "Alice", "gem", 28);

        client.FromClient(new PickupMessage { GroundItemId = gem.Id });
        fx.Ticks(1);
        Assert.Contains("You don't have enough inventory space.", client.Texts);
        Assert.NotNull(fx.World.Get<GroundItemSystem>().Find(gem.Id));
    }

    [Fact]
    public void StackableLootJoinsAPileAndViewersSeeTheNewCount()
    {
        var fx = Arena();
        FakeConnection client = fx.Login("Alice");
        GroundItemSystem ground = fx.World.Get<GroundItemSystem>();
        Player alice = fx.Player("Alice");
        GroundItem pile = ground.Spawn("coins", 4, 0, new TileCoord(7, 7), alice);
        fx.Ticks(1);
        ground.Spawn("coins", 6, 0, new TileCoord(7, 7), alice);
        fx.Ticks(1);

        Assert.Single(ground.Items);
        Assert.Equal(10, pile.Count);
        Assert.Equal(10, client.Updates.Last(u => u.GroundAdded.Count > 0).GroundAdded.Single().Count);
    }

    [Fact]
    public void DropTablesRollAlwaysEntriesNestedTablesAndNothing()
    {
        GameContent content = TestContent.Create();
        var random = new Random(9);
        int sawNothing = 0;
        for (int i = 0; i < 500; i++)
        {
            List<Drop> drops = DropRoller.Roll(content, "nested", random);
            Assert.Equal(1, drops.Count(d => d.ItemId == "gem")); // from the always-rolled nested table
            Assert.All(drops.Where(d => d.ItemId == "coins"), d => Assert.InRange(d.Count, 1, 3));
            Assert.InRange(drops.Count(d => d.ItemId == "coins"), 0, 3);
            if (drops.Count(d => d.ItemId == "coins") < 3) sawNothing++;
        }
        Assert.True(sawNothing > 0, "The nothing weight should come up sometimes.");
    }

    [Fact]
    public void TheShippedDropTablesOnlyProduceRealItems()
    {
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        var random = new Random(1);
        foreach (string table in content.DropTables.Keys)
            for (int i = 0; i < 200; i++)
                foreach (Drop drop in DropRoller.Roll(content, table, random))
                    Assert.NotNull(content.Item(drop.ItemId));
    }

    [Fact]
    public void ThousandTicksOfFightingEatingAndLootingStayConsistent()
    {
        var fx = Arena(p => p.World.StarterInventory.Add(new ItemAmount { Item = "bread", Count = 10 }),
            new NpcSpawn { Npc = "rat", X = 10, Y = 5 }, new NpcSpawn { Npc = "rat", X = 12, Y = 9 }, new NpcSpawn { Npc = "mite", X = 16, Y = 5 });
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        PlayerItems items = Items(fx, "Alice");
        GroundItemSystem ground = fx.World.Get<GroundItemSystem>();

        for (int tick = 0; tick < 1000; tick++)
        {
            if (alice.Blood < 400)
            {
                int bread = SlotOf(items.Inventory, "bread");
                if (bread >= 0) Use(client, ContainerKind.Inventory, bread, ItemActions.Consume);
            }
            if (alice.Interaction == null && !alice.IsDead)
            {
                GroundItem? loot = ground.Items.FirstOrDefault(g => ground.IsVisibleTo(g, alice));
                Npc? next = fx.World.Npcs.Where(n => !n.Despawned && !n.IsDead).OrderBy(n => n.Position.ChebyshevDistance(alice.Position)).FirstOrDefault();
                if (loot != null) client.FromClient(new PickupMessage { GroundItemId = loot.Id });
                else if (next != null) client.FromClient(new AttackMessage { TargetId = next.Id });
            }
            if (tick % 3 == 0) client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
            fx.Ticks(1);

            for (int i = 0; i < items.Inventory.Size; i++) Assert.True(items.Inventory[i].Count >= 0);
            Assert.All(ground.Items, g => Assert.True(g.Count > 0));
        }
        Assert.True(items.Inventory.CountOf("coins") >= 25, "Expected to have looted several rats' coins, got " + items.Inventory.CountOf("coins"));
    }
}
