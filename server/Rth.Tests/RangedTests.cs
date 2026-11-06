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
/// Ranged, the third combat style (docs/skills.md 3.4, docs/systems.md "Calibration"): a bow
/// fires what is worn in the quiver, one a shot, some of it landing by the target; calibration
/// builds while shooting one target from one tile; the kit's own fields (steadying, spending,
/// piercing, pinning); and the shipped kit fighting for a thousand ticks.
/// </summary>
public class RangedTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static NpcSpawn At(string npc, int x, int y) => new() { Npc = npc, X = x, Y = y, Wander = 0 };

    private static AbilityDef Shot(string id, AbilityType type, int damage, int phlegmGain = 0) => new()
    {
        Id = id, Name = id, Type = type, Style = CombatStyle.Ranged, CooldownTicks = 5, Range = 7, DamageMin = damage, DamageMax = damage, PhlegmGain = phlegmGain,
    };

    /// <summary>Test content plus a bow, a sling, arrows and stones, a ranged skill and a small ranged kit.</summary>
    private static ContentParts Parts()
    {
        List<AbilityDef> abilities = TestContent.Abilities();
        AbilityDef called = Shot("called", AbilityType.Spender, 100);
        called.CalibrationSpent = 100;
        called.PhlegmRequired = 50;
        called.PhlegmCost = 15;
        AbilityDef pierce = Shot("pierce", AbilityType.Builder, 100, 8);
        pierce.CalibrationBonus = 50;
        AbilityDef pin = Shot("pin", AbilityType.Spender, 100);
        pin.PinTicks = 3;
        pin.PhlegmRequired = 50;
        pin.PhlegmCost = 15;
        abilities.AddRange(new[]
        {
            new AbilityDef { Id = "loose", Name = "Loose", Type = AbilityType.Auto, Style = CombatStyle.Ranged, CooldownTicks = 4, Range = 7, DamageMin = 50, DamageMax = 50 },
            Shot("shot", AbilityType.Builder, 100, 8),
            pierce,
            new AbilityDef { Id = "steady", Name = "Steady", Type = AbilityType.Builder, Style = CombatStyle.Ranged, KeepsStall = true, CooldownTicks = 5, CalibrationGain = 30, PhlegmGain = 10 },
            called,
            pin,
        });
        ContentParts parts = TestContent.Parts(abilities: abilities);
        parts.AutoAttacks[CombatStyle.Ranged] = "loose";
        parts.ActionBars[CombatStyle.Ranged] = new List<string> { "shot", "pierce" };
        parts.Items.Add(new ItemDef { Id = "bow", Name = "Bow", Equipment = new EquipmentDef { Slot = EquipSlot.Weapon, Style = CombatStyle.Ranged, Ammo = "arrow" } });
        parts.Items.Add(new ItemDef { Id = "arrow", Name = "Arrow", Stackable = true, Equipment = new EquipmentDef { Slot = EquipSlot.Ammo, Stats = { [StatIds.RangedPower] = 5 } }, Ammo = new AmmoDef { Kind = "arrow" } });
        parts.Items.Add(new ItemDef { Id = "stone", Name = "Stone", Stackable = true, Equipment = new EquipmentDef { Slot = EquipSlot.Ammo }, Ammo = new AmmoDef { Kind = "stone" } });
        parts.Skills.Skills.Add(new SkillDef { Id = "ranged", Name = "Ranged", StatsPerLevel = { [StatIds.RangedPower] = 10 } });
        parts.Skills.DamageXp.Add(new DamageXpRule { Style = CombatStyle.Ranged, Skill = "ranged", XpPerDamage = 0.1 });
        parts.Npcs.Add(new NpcDef { Id = "dummy2", Name = "Second dummy", Blood = 100000, Attacks = false, Regenerates = true, WanderRadius = 0, Evasion = 0 });
        parts.Npcs.Add(new NpcDef { Id = "slippery", Name = "Slippery", Blood = 100000, Attacks = false, Regenerates = true, WanderRadius = 0, Evasion = 1.0 });
        parts.Statuses.Add(new StatusDef { Id = StatusIds.Sighted, Name = "Sighted" });
        return parts;
    }

    private sealed class Arena
    {
        public Arena(params NpcSpawn[] spawns)
        {
            Fx = new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig(), GameContent.Create(Parts()), spawns);
            Client = Fx.Login("Alice");
            Alice = Fx.Player("Alice");
        }

        public WorldFixture Fx { get; }
        public FakeConnection Client { get; }
        public Player Alice { get; }
        public CombatSystem Combat => Fx.World.Get<CombatSystem>();
        public ItemSystem Items => Fx.World.Get<ItemSystem>();
        public ItemContainer Equipment => Items.Of(Alice).Equipment;

        public void Wield(string item = "bow")
        {
            Equipment.Set((int)EquipSlot.Weapon, item, 1);
            Fx.World.RecalculateStats(Alice);
        }

        public void Quiver(string ammo, int count)
        {
            Equipment.Set((int)EquipSlot.Ammo, ammo, count);
            Fx.World.RecalculateStats(Alice);
        }

        public int InQuiver => Equipment.IsEmpty((int)EquipSlot.Ammo) ? 0 : Equipment[(int)EquipSlot.Ammo].Count;

        public void Attack(string npc)
        {
            Client.FromClient(new AttackMessage { TargetId = Fx.Npc(npc).Id });
            Fx.Ticks(1);
        }

        public void Use(string ability, int ticks = 1)
        {
            Client.FromClient(new UseAbilityMessage { AbilityId = ability });
            Fx.Ticks(ticks);
        }

        public int OnGround(string item, TileCoord tile) =>
            Fx.World.Get<GroundItemSystem>().Items.Where(g => g.ItemId == item && g.Position == tile).Sum(g => g.Count);
    }

    // ------------------------------------------------------------------ the quiver

    [Fact]
    public void ABowShootsOnlyWithItsOwnAmmunitionInTheQuiver()
    {
        var arena = new Arena(At("dummy", 10, 5));
        Npc dummy = arena.Fx.Npc("dummy");
        arena.Wield();
        Assert.Equal(CombatStyle.Ranged, arena.Combat.WeaponStyle(arena.Alice));
        Assert.Equal("loose", arena.Combat.AutoAttack(arena.Alice).Id);

        arena.Attack("dummy");
        Assert.Contains("You have no arrows for your bow.", arena.Client.Texts);
        Assert.Null(arena.Alice.Interaction);
        Assert.Equal(dummy.MaxBlood, dummy.Blood);

        arena.Quiver("stone", 10);
        arena.Attack("dummy");
        Assert.Contains("Your bow fires arrows, not stones.", arena.Client.Texts);
        arena.Use("shot");
        Assert.Equal(dummy.MaxBlood, dummy.Blood);

        // Wielding a stack from the pack puts the whole stack in the quiver, joining more of the same.
        arena.Items.Give(arena.Alice, "arrow", 20);
        int slot = arena.Items.Of(arena.Alice).Inventory.Snapshot().FindIndex(s => s.ItemId == "arrow");
        arena.Client.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = slot, ActionId = ItemActions.Equip });
        arena.Fx.Ticks(1);
        Assert.Equal(20, arena.InQuiver);
        Assert.Equal(10, arena.Items.CountOf(arena.Alice, "stone")); // the stones came back to the pack
        arena.Items.Give(arena.Alice, "arrow", 5);
        slot = arena.Items.Of(arena.Alice).Inventory.Snapshot().FindIndex(s => s.ItemId == "arrow");
        arena.Client.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = slot, ActionId = ItemActions.Equip });
        arena.Fx.Ticks(1);
        Assert.Equal(25, arena.InQuiver);
        Assert.Equal(5, arena.Alice.Stats.Get(StatIds.RangedPower)); // the arrows' power counts while worn (level 1 adds none)

        arena.Attack("dummy");
        Assert.True(dummy.Blood < dummy.MaxBlood, "the bow didn't shoot");
        Assert.Equal(Spawn, arena.Alice.Position); // from where she stands: range 7
    }

    [Fact]
    public void EachShotUsesOneAndSomeLandByTheTarget()
    {
        var arena = new Arena(At("dummy", 10, 5));
        arena.Wield();
        arena.Quiver("arrow", 20);
        arena.Attack("dummy");
        arena.Fx.Ticks(39); // ten auto-attacks at a cooldown of 4
        Assert.Equal(10, arena.InQuiver);
        int landed = arena.OnGround("arrow", new TileCoord(10, 5));
        Assert.True(landed > 0 && landed < 10, "arrows recovered: " + landed);

        // The last arrow stops the fight, not the client.
        arena.Quiver("arrow", 1);
        arena.Fx.Ticks(4);
        Assert.Equal(0, arena.InQuiver);
        arena.Fx.Ticks(4);
        Assert.Contains("You have no arrows for your bow.", arena.Client.Texts);
        Assert.Null(arena.Alice.Interaction);
        Assert.Equal(0, arena.Alice.Stats.Get(StatIds.RangedPower)); // the empty quiver gives nothing
    }

    // ------------------------------------------------------------------ calibration

    [Fact]
    public void CalibrationBuildsOnOneTargetFromOneTileAndFallsWithMovesMissesAndRest()
    {
        var arena = new Arena(At("dummy", 10, 5), At("dummy2", 9, 7), At("slippery", 11, 6));
        CombatSystem combat = arena.Combat;
        arena.Wield();
        arena.Quiver("arrow", 500);

        arena.Attack("dummy");
        Assert.Equal(10, combat.Calibration(arena.Alice)); // the first hit
        arena.Fx.Ticks(4);
        Assert.Equal(20, combat.Calibration(arena.Alice));
        Assert.Contains(arena.Fx.World.StatusesOf(arena.Alice), s => s.Id == StatusIds.Sighted && s.Magnitude == 20);

        // A step halves it.
        arena.Client.FromClient(new WalkToMessage { X = 6, Y = 5 });
        arena.Fx.Ticks(1);
        Assert.Equal(10, combat.Calibration(arena.Alice));

        // Another target starts from nothing.
        arena.Attack("dummy2");
        arena.Fx.Ticks(2); // the auto-attack's cooldown runs on across targets: the first shot at it
        Assert.Equal(10, combat.Calibration(arena.Alice)); // 0, then the hit's 10
        arena.Fx.Ticks(4);
        Assert.Equal(20, combat.Calibration(arena.Alice));

        // Misses cost it: a slippery target is hit one time in twenty.
        arena.Attack("slippery");
        arena.Fx.Ticks(40);
        Assert.True(combat.Calibration(arena.Alice) < 20, "calibration " + combat.Calibration(arena.Alice));

        // Out of combat it is gone.
        arena.Attack("dummy");
        arena.Fx.Ticks(4);
        Assert.True(combat.Calibration(arena.Alice) > 0);
        arena.Fx.World.StopInteraction(arena.Alice);
        arena.Fx.Ticks(12);
        Assert.Equal(0, combat.Calibration(arena.Alice));
    }

    [Fact]
    public void CalibrationMakesShotsLandHarderAndACalledShotSpendsIt()
    {
        var arena = new Arena(At("dummy", 10, 5));
        CombatSystem combat = arena.Combat;
        Npc dummy = arena.Fx.Npc("dummy");
        arena.Wield();
        arena.Quiver("arrow", 500);
        arena.Alice.Phlegm = 100;
        arena.Attack("dummy");
        arena.Use("steady", 1); // keepsStall: used on herself, nothing shot
        arena.Fx.Ticks(3);
        Assert.True(combat.Calibration(arena.Alice) >= 40, "calibration " + combat.Calibration(arena.Alice));

        // Pierce: +10% from the meter itself, up to +50% more from its own bonus; the meter is kept.
        int c = combat.Calibration(arena.Alice);
        int before = dummy.Blood;
        arena.Use("pierce");
        // 100 ability damage + 5 ranged power from the arrows (level 1 adds none) = 105 a hit.
        int dealt = before - dummy.Blood;
        Assert.Equal((int)System.Math.Round(105 * (1 + (CombatRules.Calibrated(c, 10) + CombatRules.Calibrated(c, 50)) / 100.0)), dealt);
        Assert.Equal(c + 10, combat.Calibration(arena.Alice));

        // The called shot spends all of it.
        arena.Fx.Ticks(2);
        c = combat.Calibration(arena.Alice);
        arena.Alice.Phlegm = 100;
        before = dummy.Blood;
        arena.Use("called");
        dealt = before - dummy.Blood;
        Assert.Equal((int)System.Math.Round(105 * (1 + (CombatRules.Calibrated(c, 10) + c) / 100.0)), dealt);
        Assert.Equal(0, combat.Calibration(arena.Alice));
    }

    [Fact]
    public void APinningShotHoldsTheTargetWhereItStands()
    {
        var arena = new Arena(At("rat", 11, 5));
        Npc rat = arena.Fx.Npc("rat");
        arena.Wield();
        arena.Quiver("arrow", 500);
        arena.Alice.Phlegm = 100;
        arena.Attack("rat");
        arena.Use("pin");
        Assert.True(rat.RootedUntilTick > arena.Fx.World.CurrentTick);
        TileCoord held = rat.Position;
        arena.Fx.Ticks(2);
        Assert.Equal(held, rat.Position); // it wants to close in, and can't
        arena.Fx.Ticks(3);
        Assert.NotEqual(held, rat.Position);
    }

    // ------------------------------------------------------------------ training and content

    [Fact]
    public void RangedDamageTrainsRangedAndItsLevelsAddRangedPowerOnly()
    {
        var arena = new Arena(At("dummy", 10, 5));
        SkillSystem skills = arena.Fx.World.Get<SkillSystem>();
        arena.Wield();
        arena.Quiver("arrow", 500);
        arena.Attack("dummy");
        arena.Fx.Ticks(40);
        Assert.True(skills.Xp(arena.Alice, "ranged") > 0);
        Assert.Equal(0, skills.Xp(arena.Alice, "occultism"));
        Assert.Equal(0, arena.Alice.Stats.Get(StatIds.RangedPower) - 5); // level 1: nothing beyond the arrows
        Assert.Equal(0, arena.Alice.Stats.Get(StatIds.MeleePower));

        skills.AddXp(arena.Alice, "ranged", arena.Fx.World.Content.XpCurve.XpForLevel(5));
        Assert.Equal(40, arena.Alice.Stats.Get(StatIds.RangedPower) - 5); // 10 a level above the first
    }

    [Fact]
    public void AmmunitionAndRangedWeaponsAreValidated()
    {
        ContentParts parts = Parts();
        parts.Items.Add(new ItemDef { Id = "bad_bow", Name = "Bad bow", Equipment = new EquipmentDef { Slot = EquipSlot.Weapon, Style = CombatStyle.Ranged } });
        parts.Items.Add(new ItemDef { Id = "loose_arrows", Name = "Loose arrows", Equipment = new EquipmentDef { Slot = EquipSlot.Ammo }, Ammo = new AmmoDef { Kind = "arrow" } });
        parts.Items.Add(new ItemDef { Id = "stack_hat", Name = "Stack hat", Stackable = true, Equipment = new EquipmentDef { Slot = EquipSlot.Head } });
        var e = Assert.Throws<ContentException>(() => GameContent.Create(parts));
        Assert.Contains("'bad_bow'", e.Message);
        Assert.Contains("'loose_arrows'", e.Message);
        Assert.Contains("'stack_hat'", e.Message);
    }

    [Fact]
    public void TheShippedRangedKitFightsForAThousandTicks()
    {
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig(), content, new[] { At("straw_penitent", 10, 5) });
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        ItemSystem items = fx.World.Get<ItemSystem>();
        CombatSystem combat = fx.World.Get<CombatSystem>();
        Assert.Equal(1, items.CountOf(alice, "hunting_sling")); // the starter kit has a sling and stones
        Assert.Equal(60, items.CountOf(alice, "sling_stone"));
        items.Of(alice).Equipment.Set((int)EquipSlot.Weapon, "hunting_sling", 1);
        items.Of(alice).Equipment.Set((int)EquipSlot.Ammo, "sling_stone", 400);
        fx.World.RecalculateStats(alice);
        Assert.Equal("loose", combat.AutoAttack(alice).Id);
        Assert.Equal(content.ActionBars[CombatStyle.Ranged], combat.MainBarAbilities(alice));

        string[] rotation = { "snap_shot", "barbed_shot", "piercing_shot", "steady_aim", "called_shot", "pinning_shot", "rain_of_barbs", "deadeye" };
        Npc penitent = fx.Npc("straw_penitent");
        int kills = 0;
        for (int i = 0; i < 1000; i++)
        {
            // The effigy falls and comes back: take it up again each time.
            if (alice.TargetId == 0 && !penitent.IsDead && !penitent.Despawned)
            {
                client.FromClient(new AttackMessage { TargetId = penitent.Id });
                if (i > 0) kills++;
            }
            if (i % 3 == 0) client.FromClient(new UseAbilityMessage { AbilityId = rotation[i / 3 % rotation.Length] });
            fx.Ticks(1);
        }

        SkillSystem skills = fx.World.Get<SkillSystem>();
        Assert.True(skills.Level(alice, "ranged") >= 3, "ranged level " + skills.Level(alice, "ranged") + ", kills " + kills);
        Assert.Equal(0, skills.Xp(alice, "melee"));
        int left = items.Of(alice).Equipment[(int)EquipSlot.Ammo].Count;
        Assert.True(left < 400 && left > 0, "stones left " + left);
        Assert.True(fx.World.Get<GroundItemSystem>().Items.Any(g => g.ItemId == "sling_stone"), "no stones landed by the target");
        Assert.False(alice.IsDead);
        Assert.Equal(alice.Position, Spawn); // never had to move: it all happens at range
    }
}
