using System.Collections.Generic;
using System.Linq;
using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>Medicine's heal over time (docs/gdd/systems/blood.md): a salve gives blood back a little each tick, in combat too.</summary>
public class MedicineTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static ContentParts Parts()
    {
        ContentParts parts = TestContent.Parts();
        parts.Items.Add(new ItemDef
        {
            Id = "salve", Name = "Salve",
            Consumable = new ConsumableDef { Verb = "Apply", CooldownGroup = "salve", CooldownTicks = 5, AttackDelayTicks = 1, Effects = { new EffectOp { Op = EffectOp.HealOverTime, Amount = 100, Ticks = 10 } } },
        });
        parts.Statuses.Add(new StatusDef { Id = StatusIds.Mending, Name = "Mending" });
        return parts;
    }

    [Fact]
    public void ASalveHealsALittleEachTickEvenInCombat()
    {
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig(), GameContent.Create(Parts()), new[] { new NpcSpawn { Npc = "dummy", X = 7, Y = 5, Wander = 0 } });
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        ItemSystem items = fx.World.Get<ItemSystem>();
        CombatSystem combat = fx.World.Get<CombatSystem>();
        items.Give(alice, "salve", 1);
        int slot = items.Of(alice).Inventory.Snapshot().FindIndex(s => s.ItemId == "salve");

        // In combat, so no natural regeneration muddies the count.
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        fx.Ticks(1);
        alice.Blood = 500;
        client.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = slot, ActionId = ItemActions.Consume });
        fx.Ticks(1);
        Assert.Equal(510, alice.Blood); // the first tick's share
        Assert.Equal(90, combat.Mending(alice));
        Assert.Contains(fx.World.StatusesOf(alice), s => s.Id == StatusIds.Mending && s.Magnitude == 90);
        fx.Ticks(9);
        Assert.Equal(600, alice.Blood);
        Assert.Equal(0, combat.Mending(alice));
        Assert.DoesNotContain(fx.World.StatusesOf(alice), s => s.Id == StatusIds.Mending);
        Assert.Equal(0, items.CountOf(alice, "salve"));
    }

    [Fact]
    public void AHealOverTimeNeedsAnAmountAndTicks()
    {
        ContentParts parts = Parts();
        parts.Items.Add(new ItemDef { Id = "bad_salve", Name = "Bad salve", Consumable = new ConsumableDef { Effects = { new EffectOp { Op = EffectOp.HealOverTime, Amount = 50 } } } });
        var e = Assert.Throws<ContentException>(() => GameContent.Create(parts));
        Assert.Contains("'bad_salve'", e.Message);
    }
}
