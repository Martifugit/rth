using System.Collections.Generic;
using System.Linq;
using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Server.Items;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Skills;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>
/// Tipped ammunition (tips.json, docs/skills.md 3.4): tipped items generated from plain ones,
/// tipped by using tips on them, set off by landed shots more often with a steady aim, and
/// coming back plain when recovered.
/// </summary>
public class TipTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static ContentParts Parts(TipEffect effect, double chance = 100, double calibrated = 0, int recover = 0)
    {
        List<AbilityDef> abilities = TestContent.Abilities();
        abilities.Add(new AbilityDef { Id = "loose", Name = "Loose", Type = AbilityType.Auto, Style = CombatStyle.Ranged, CooldownTicks = 4, Range = 7, DamageMin = 50, DamageMax = 50 });
        CombatRules rules = TestContent.Rules();
        rules.AmmoRecoverPercent = recover;
        ContentParts parts = TestContent.Parts(rules, abilities);
        parts.AutoAttacks[CombatStyle.Ranged] = "loose";
        parts.Items.Add(new ItemDef { Id = "bow", Name = "Bow", Equipment = new EquipmentDef { Slot = EquipSlot.Weapon, Style = CombatStyle.Ranged, Ammo = "arrow" } });
        parts.Items.Add(new ItemDef { Id = "arrow", Name = "Arrow", Stackable = true, Value = 2, Equipment = new EquipmentDef { Slot = EquipSlot.Ammo, Stats = { [StatIds.RangedPower] = 5 } }, Ammo = new AmmoDef { Kind = "arrow", Tier = 2 } });
        parts.Items.Add(new ItemDef { Id = "stone", Name = "Stone", Stackable = true, Equipment = new EquipmentDef { Slot = EquipSlot.Ammo }, Ammo = new AmmoDef { Kind = "stone" } });
        parts.Items.Add(new ItemDef { Id = "garnet_tips", Name = "Garnet tips", Stackable = true, Value = 5 });
        parts.Skills.Skills.Add(new SkillDef { Id = "artifice", Name = "Artifice" });
        parts.Tips = new TipsFile
        {
            Tips = { new TipDef { Id = "garnet", Name = "Garnet", Tip = "garnet_tips", Ammo = { "arrow" }, Level = 1, Xp = 2, Batch = 5, Chance = chance, ChanceCalibrated = calibrated, Effect = effect } },
        };
        return parts;
    }

    private sealed class Arena
    {
        public Arena(ContentParts parts, params NpcSpawn[] spawns)
        {
            Fx = new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig(), GameContent.Create(parts), spawns);
            Client = Fx.Login("Alice");
            Alice = Fx.Player("Alice");
            Items.Of(Alice).Equipment.Set((int)EquipSlot.Weapon, "bow", 1);
            Fx.World.RecalculateStats(Alice);
        }

        public WorldFixture Fx { get; }
        public FakeConnection Client { get; }
        public Player Alice { get; }
        public ItemSystem Items => Fx.World.Get<ItemSystem>();

        public void Quiver(string ammo, int count)
        {
            Items.Of(Alice).Equipment.Set((int)EquipSlot.Ammo, ammo, count);
            Fx.World.RecalculateStats(Alice);
        }

        public void Attack(string npc)
        {
            Client.FromClient(new AttackMessage { TargetId = Fx.Npc(npc).Id });
            Fx.Ticks(1);
        }
    }

    private static NpcSpawn At(string npc, int x, int y) => new() { Npc = npc, X = x, Y = y, Wander = 0 };

    [Fact]
    public void EachTipMakesATippedVersionOfEveryAmmunitionItFits()
    {
        GameContent content = GameContent.Create(Parts(new TipEffect { HealPercent = 10 }));
        ItemDef tipped = content.Item("arrow.garnet")!;
        Assert.Equal("Garnet-tipped arrow", tipped.Name);
        Assert.Equal("garnet", tipped.Ammo!.Tip);
        Assert.Equal("arrow", tipped.Ammo.Plain);
        Assert.Equal("arrow", tipped.Ammo.Kind);
        Assert.Equal(2, tipped.Ammo.Tier);
        Assert.Equal(5, tipped.Equipment!.Stats[StatIds.RangedPower]); // fires and hits like the plain one
        Assert.Equal(7, tipped.Value);
        Assert.Null(content.Item("stone.garnet")); // the tip fits arrows only
        Assert.True(content.Recipes.ContainsKey("tip_arrow.garnet"));
        Assert.Contains(content.Uses, u => u.Item == "garnet_tips" && u.OnItem == "arrow" && u.Recipe == "tip_arrow.garnet");
    }

    [Fact]
    public void UsingTipsOnArrowsTipsThemInBatches()
    {
        var arena = new Arena(Parts(new TipEffect { HealPercent = 10 }));
        arena.Items.Give(arena.Alice, "arrow", 12);
        arena.Items.Give(arena.Alice, "garnet_tips", 10);
        List<ItemSlot> pack = arena.Items.Of(arena.Alice).Inventory.Snapshot();
        int tips = pack.FindIndex(s => s.ItemId == "garnet_tips"), arrows = pack.FindIndex(s => s.ItemId == "arrow");
        arena.Client.FromClient(new UseItemMessage { Slot = tips, ItemId = "garnet_tips", Target = UseTarget.Item, TargetSlot = arrows, TargetItemId = "arrow" });
        arena.Fx.Ticks(12);
        Assert.Equal(10, arena.Items.CountOf(arena.Alice, "arrow.garnet")); // two batches: the tips ran out
        Assert.Equal(2, arena.Items.CountOf(arena.Alice, "arrow"));
        Assert.Equal(0, arena.Items.CountOf(arena.Alice, "garnet_tips"));
    }

    [Fact]
    public void ALandedShotSetsOffTheTip()
    {
        var arena = new Arena(Parts(new TipEffect { Message = "It drinks.", HealPercent = 50, DamagePercent = 40, PinTicks = 3 }), At("dummy", 10, 5));
        arena.Quiver("arrow.garnet", 10);
        arena.Alice.Blood = arena.Alice.MaxBlood / 2;
        int before = arena.Alice.Blood;
        Npc dummy = arena.Fx.Npc("dummy");
        arena.Attack("dummy");
        int dealt = dummy.MaxBlood - dummy.Blood;
        Assert.True(dealt > 0);
        Assert.Contains("It drinks.", arena.Client.Texts);
        // The shot and the tip's second hit of 40% of it.
        int first = (int)System.Math.Round(dealt / 1.4);
        Assert.InRange(dealt - first, first * 40 / 100 - 1, first * 40 / 100 + 1);
        Assert.InRange(arena.Alice.Blood - before, first / 2 - 1, first / 2 + 1);
        Assert.True(dummy.RootedUntilTick > arena.Fx.World.CurrentTick);
    }

    [Fact]
    public void PlainArrowsAndMissesSetOffNothing()
    {
        var arena = new Arena(Parts(new TipEffect { Message = "It drinks.", HealPercent = 50 }), At("dummy", 10, 5));
        arena.Quiver("arrow", 10);
        arena.Attack("dummy");
        arena.Fx.Ticks(8);
        Assert.DoesNotContain("It drinks.", arena.Client.Texts);
    }

    [Fact]
    public void ASplashHitsTheCreaturesAroundTheTarget()
    {
        var arena = new Arena(Parts(new TipEffect { SplashRadius = 1, SplashPercent = 50 }), At("dummy", 10, 5), At("rat", 11, 6), At("rat", 13, 5));
        arena.Quiver("arrow.garnet", 10);
        arena.Attack("dummy");
        Npc[] rats = arena.Fx.World.Npcs.Where(n => n.Def.Id == "rat").OrderBy(n => n.Position.X).ToArray();
        Assert.True(rats[0].Blood < rats[0].MaxBlood); // beside the target
        Assert.Equal(rats[1].MaxBlood, rats[1].Blood); // three tiles off
    }

    [Fact]
    public void ASteadyAimSetsTheTipOffMoreOften()
    {
        var tip = new TipDef { Chance = 8, ChanceCalibrated = 12 };
        Assert.Equal(8, CombatSystem.TipChance(tip, 0));
        Assert.Equal(14, CombatSystem.TipChance(tip, 50));
        Assert.Equal(20, CombatSystem.TipChance(tip, 100));
    }

    [Fact]
    public void ATippedArrowComesBackPlain()
    {
        var arena = new Arena(Parts(new TipEffect { HealPercent = 10 }, recover: 100), At("dummy", 10, 5));
        arena.Quiver("arrow.garnet", 10);
        arena.Attack("dummy");
        var ground = arena.Fx.World.Get<GroundItemSystem>().Items.ToList();
        Assert.Contains(ground, g => g.ItemId == "arrow");
        Assert.DoesNotContain(ground, g => g.ItemId == "arrow.garnet");
    }

    [Fact]
    public void ATipMustNameRealTipsAndDoSomething()
    {
        ContentParts parts = Parts(new TipEffect());
        parts.Tips.Tips[0].Tip = "nothing";
        var e = Assert.Throws<ContentException>(() => GameContent.Create(parts));
        Assert.Contains("tips item 'nothing' doesn't exist", e.Message);
        Assert.Contains("its effect does nothing", e.Message);
    }

    [Fact]
    public void TheShippedTipsLoad()
    {
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        Assert.True(content.Tips.Count >= 3);
        foreach (TipDef tip in content.Tips.Values)
            Assert.Contains(content.Items.Values, i => i.Ammo?.Tip == tip.Id);
        Assert.NotNull(content.Item("copper_arrow.garnet"));
    }
}
