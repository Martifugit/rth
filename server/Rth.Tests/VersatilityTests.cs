using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>Versatility: fighting in more than one style lately adds to every hit (GDD combat style, rule 3).</summary>
public class VersatilityTests
{
    private static WorldFixture Arena(int window)
    {
        CombatRules rules = TestContent.Rules();
        rules.VersatilityWindowTicks = window;
        rules.VersatilityPercentPerStyle = 10;
        List<AbilityDef> abilities = TestContent.Abilities();
        abilities.Add(new AbilityDef { Id = "chop", Name = "Chop", Type = AbilityType.Auto, Style = CombatStyle.Melee, CooldownTicks = 4, Range = 1, DamageMin = 40, DamageMax = 40 });
        ContentParts parts = TestContent.Parts(rules, abilities);
        parts.AutoAttacks[CombatStyle.Melee] = "chop";
        parts.Items.Add(new ItemDef { Id = "blade", Name = "Blade", Equipment = new EquipmentDef { Slot = EquipSlot.Weapon, Style = CombatStyle.Melee } });
        return new WorldFixture(Maps.Open(40, 40, new TileCoord(5, 5)), new ServerConfig(), GameContent.Create(parts),
            new[] { new NpcSpawn { Npc = "dummy", X = 6, Y = 5, Wander = 0 } });
    }

    [Fact]
    public void SwitchingStylesAddsToTheNextHits()
    {
        var fx = Arena(window: 20);
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Npc dummy = fx.Npc("dummy");
        CombatSystem combat = fx.World.Get<CombatSystem>();

        client.FromClient(new AttackMessage { TargetId = dummy.Id });
        fx.Ticks(1);
        Assert.Equal(dummy.MaxBlood - 50, dummy.Blood); // the occult auto-attack, one style: no bonus
        Assert.Equal(0, combat.Versatility(alice));

        // Draw a blade: the melee auto-attack comes next, now two styles in the window.
        fx.World.Get<ItemSystem>().Of(alice).Equipment.Set((int)EquipSlot.Weapon, "blade", 1);
        fx.World.RecalculateStats(alice);
        int before = dummy.Blood;
        fx.Ticks(4);
        Assert.Equal(before - 44, dummy.Blood); // 40, and 10% more
        Assert.Equal(10, combat.Versatility(alice));
        Assert.Contains(fx.World.StatusesOf(alice), s => s.Id == StatusIds.Versatile && s.Magnitude == 10);

        // Stay with the blade: the occult use falls out of the window and the bonus goes.
        fx.Ticks(20);
        Assert.Equal(0, combat.Versatility(alice));
    }

    [Fact]
    public void OffWhenTheWindowIsZero()
    {
        var fx = Arena(window: 0);
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        fx.Ticks(1);
        fx.World.Get<ItemSystem>().Of(alice).Equipment.Set((int)EquipSlot.Weapon, "blade", 1);
        fx.World.RecalculateStats(alice);
        fx.Ticks(4);
        Assert.Equal(0, fx.World.Get<CombatSystem>().Versatility(alice));
    }
}
