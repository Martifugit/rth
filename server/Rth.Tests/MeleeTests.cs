using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Progress;
using Rth.Shared.Skills;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>The melee style (weapons, abilities, bleeds) and Cruor's verses.</summary>
public class MeleeTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static NpcSpawn At(string npc, int x, int y) => new() { Npc = npc, X = x, Y = y, Wander = 0 };

    private static AbilityDef Verse(string id, BuffDef buff, string scripture = "scripture", int bloodCurrent = 0, int bloodMax = 0) => new()
    {
        Id = id, Name = id, Type = AbilityType.Verse, Style = CombatStyle.Melee, CooldownTicks = 10,
        BloodCostCurrent = bloodCurrent, BloodCostMax = bloodMax, Buff = buff,
        Reagents = { new ItemCost { Item = scripture } },
        Xp = { new XpReward { Skill = "religion", Amount = 5 } },
    };

    /// <summary>Test content plus a melee weapon, melee abilities, verses and an occult-made creature.</summary>
    private static ContentParts Parts()
    {
        List<AbilityDef> abilities = TestContent.Abilities();
        abilities.AddRange(new[]
        {
            new AbilityDef { Id = "chop", Name = "Chop", Type = AbilityType.Auto, Style = CombatStyle.Melee, CooldownTicks = 4, Range = 1, DamageMin = 40, DamageMax = 40 },
            new AbilityDef { Id = "hew", Name = "Hew", Type = AbilityType.Builder, Style = CombatStyle.Melee, CooldownTicks = 5, Range = 1, DamageMin = 100, DamageMax = 100, PhlegmGain = 8 },
            new AbilityDef { Id = "let_blood", Name = "Let Blood", Type = AbilityType.Builder, Style = CombatStyle.Melee, CooldownTicks = 5, Range = 1, DamageMin = 100, DamageMax = 100, BleedPercent = 100, BleedTicks = 4 },
            new AbilityDef { Id = "scourge", Name = "Scourge", Type = AbilityType.Builder, Style = CombatStyle.Melee, CooldownTicks = 5, Range = 1, DamageMin = 100, DamageMax = 100, LowBloodBonus = 100 },
            new AbilityDef { Id = "reap", Name = "Reap", Type = AbilityType.Ultimate, Style = CombatStyle.Melee, CooldownTicks = 30, Range = 1, DamageMin = 400, DamageMax = 400, PhlegmRequired = 100, PhlegmCost = 100 },
            Verse("siphon", new BuffDef { Status = "siphon", Ticks = 10, Stats = { [StatIds.Lifesteal] = 20 } }, bloodCurrent: 10),
            Verse("abjure", new BuffDef { Status = "abjure", Ticks = 10, TargetTag = "occult-made", Stats = { [StatIds.DamageBonus] = 300 }, Otherwise = { [StatIds.DamageBonus] = -100 } }),
            Verse("dance", new BuffDef { Status = "dance", Ticks = 20, Stats = { [StatIds.DeferDamage] = 20 } }),
            Verse("spike", new BuffDef { Status = "spike", Ticks = 12, Stats = { [StatIds.BuilderPhlegm] = 2, [StatIds.BloodPerAbility] = 2 } }),
            Verse("redeem", new BuffDef { Status = "redeem", Ticks = 10, EndsOnHitTaken = true, Stats = { [StatIds.BlockHit] = 100, [StatIds.BlockedPhlegm] = 50 } }, bloodCurrent: 70),
            Verse("martyr", new BuffDef { Status = "martyr", Ticks = 50, Stats = { [StatIds.ScarOnHit] = 1 } }),
            Verse("stigmata", new BuffDef { Status = "stigmata", Ticks = 20, WhileBloodBelow = 30, Stats = { [StatIds.DamageBonus] = 30 } }, bloodMax: 80),
        });
        ContentParts parts = TestContent.Parts(abilities: abilities);
        parts.AutoAttacks[CombatStyle.Melee] = "chop";
        parts.ActionBars[CombatStyle.Melee] = new List<string> { "let_blood", "hew" };
        parts.Items.Add(new ItemDef { Id = "blade", Name = "Blade", Equipment = new EquipmentDef { Slot = EquipSlot.Weapon, Style = CombatStyle.Melee } });
        parts.Items.Add(new ItemDef { Id = "scripture", Name = "Scripture", Stackable = true });
        parts.Skills.Skills.Add(new SkillDef { Id = "melee", Name = "Melee", StatsPerLevel = { [StatIds.MeleePower] = 10 } });
        parts.Skills.DamageXp.Add(new DamageXpRule { Style = CombatStyle.Melee, Skill = "melee", XpPerDamage = 0.1 });
        parts.Npcs.Add(new NpcDef { Id = "mite_dummy", Name = "Made thing", Blood = 100000, Attacks = false, Regenerates = true, WanderRadius = 0, Evasion = 0, Tags = { "occult-made" } });
        foreach (string status in new[] { "siphon", "abjure", "dance", "spike", "redeem", "martyr", "stigmata" })
            parts.Statuses.Add(new StatusDef { Id = status, Name = status });
        return parts;
    }

    private sealed class Arena
    {
        public Arena(params NpcSpawn[] spawns)
        {
            Fx = new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig(), GameContent.Create(Parts()), spawns);
            Client = Fx.Login("Alice");
            Alice = Fx.Player("Alice");
            Fx.World.Get<SkillSystem>().AddXp(Alice, "defence", Fx.World.Content.XpCurve.XpForLevel(11)); // 1,900 blood: room to pay for verses
            Alice.Blood = Alice.MaxBlood;
        }

        public WorldFixture Fx { get; }
        public FakeConnection Client { get; }
        public Player Alice { get; }
        public CombatSystem Combat => Fx.World.Get<CombatSystem>();

        public void Wield(string item = "blade")
        {
            Fx.World.Get<ItemSystem>().Of(Alice).Equipment.Set((int)EquipSlot.Weapon, item, 1);
            Fx.World.RecalculateStats(Alice);
        }

        public void Scriptures(int count) => Fx.World.Get<ItemSystem>().Give(Alice, "scripture", count);

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

        /// <summary>Lets the global cooldown and the auto-attack pass without anything firing (the target doesn't fight back).</summary>
        public void Wait(int ticks) => Fx.Ticks(ticks);
    }

    // ------------------------------------------------------------------ the style

    [Fact]
    public void TheWeaponDecidesTheAutoAttack()
    {
        var arena = new Arena(At("dummy", 10, 5));
        Npc dummy = arena.Fx.Npc("dummy");
        arena.Attack("dummy");
        Assert.Equal(dummy.MaxBlood - 50, dummy.Blood); // zap, from where she stands
        Assert.Equal(Spawn, arena.Alice.Position);

        arena.Fx.World.StopInteraction(arena.Alice);
        arena.Wield();
        arena.Fx.Ticks(4);
        int before = dummy.Blood;
        arena.Attack("dummy");
        arena.Fx.Ticks(6);
        Assert.Equal(1, arena.Alice.Position.ChebyshevDistance(dummy.Position)); // walked up to it
        int dealt = before - dummy.Blood;
        Assert.True(dealt >= 40 && dealt % 40 == 0, "chops of 40, not zaps of 50: " + dealt);
        Assert.Equal(CombatStyle.Melee, arena.Combat.WeaponStyle(arena.Alice));
    }

    [Fact]
    public void EachStyleHasItsOwnMainBarForRevolution()
    {
        var arena = new Arena(At("dummy", 6, 5));
        CombatSystem combat = arena.Combat;
        Assert.Equal(arena.Fx.World.Content.ActionBar, combat.MainBarAbilities(arena.Alice));
        arena.Wield();
        Assert.Equal(new[] { "let_blood", "hew" }, combat.MainBarAbilities(arena.Alice)); // the melee default

        arena.Client.FromClient(new SetClientStateMessage { Key = CombatOptions.MainBarKey(CombatStyle.Melee), Json = "[\"a:hew\", null]" });
        arena.Client.FromClient(new SetClientStateMessage { Key = CombatOptions.RevolutionKey, Json = "builders" });
        arena.Attack("dummy");
        arena.Fx.Ticks(4);
        Assert.Equal(new[] { "hew" }, combat.MainBarAbilities(arena.Alice));
        Assert.True(arena.Alice.Cooldowns.ContainsKey("hew"));
    }

    [Fact]
    public void AbilitiesNeedAWeaponOfTheirStyle()
    {
        var arena = new Arena(At("dummy", 6, 5));
        arena.Attack("dummy");
        arena.Use("hew");
        Assert.Contains(arena.Client.Texts, t => t == "Hew needs a melee weapon.");

        arena.Wield();
        arena.Use("spark");
        Assert.Contains(arena.Client.Texts, t => t.StartsWith("Spark needs an occult weapon"));
    }

    [Fact]
    public void MeleeDamageTrainsMeleeAndItsLevelsAddMeleePowerOnly()
    {
        var arena = new Arena(At("dummy", 6, 5));
        arena.Wield();
        SkillSystem skills = arena.Fx.World.Get<SkillSystem>();
        Npc dummy = arena.Fx.Npc("dummy");
        arena.Attack("dummy");
        Assert.Equal(dummy.MaxBlood - 40, dummy.Blood); // the opening chop
        arena.Use("hew");
        Assert.Equal(dummy.MaxBlood - 140, dummy.Blood);
        Assert.Equal(14, skills.Xp(arena.Alice, "melee"), 3);

        skills.AddXp(arena.Alice, "melee", arena.Fx.World.Content.XpCurve.XpForLevel(2));
        Assert.Equal(10, arena.Alice.Stats.Get(StatIds.MeleePower));
        Assert.Equal(100, arena.Alice.Stats.Get(StatIds.AbilityDamage)); // other styles don't gain from it
    }

    [Fact]
    public void ABleedKeepsDamagingAndCountsAsTheAttackers()
    {
        var arena = new Arena(At("dummy", 6, 5));
        arena.Wield();
        Npc dummy = arena.Fx.Npc("dummy");
        arena.Attack("dummy");
        int before = dummy.Blood;
        arena.Use("let_blood");
        Assert.Equal(before - 100, dummy.Blood);
        arena.Fx.World.StopInteraction(arena.Alice); // no more attacks: only the bleed
        arena.Fx.Ticks(6);
        Assert.Equal(before - 200, dummy.Blood); // 100 more, over 4 ticks
        Assert.Empty(dummy.Bleeds);
        Assert.Equal(24, arena.Fx.World.Get<SkillSystem>().Xp(arena.Alice, "melee"), 3); // the chop, the cut and the bleed
    }

    [Fact]
    public void LowBloodMakesSomeAbilitiesHitHarder()
    {
        var arena = new Arena(At("dummy", 6, 5));
        arena.Wield();
        Npc dummy = arena.Fx.Npc("dummy");
        arena.Alice.Blood = arena.Alice.MaxBlood / 2;
        arena.Attack("dummy");
        int before = dummy.Blood;
        arena.Use("scourge");
        Assert.Equal(before - 150, dummy.Blood); // +50% at half blood
    }

    // ------------------------------------------------------------------ verses

    [Fact]
    public void AVerseCostsBloodAndAScriptureAndTrainsReligion()
    {
        var arena = new Arena(At("dummy", 6, 5));
        arena.Wield();
        arena.Attack("dummy");
        arena.Wait(3);
        arena.Use("siphon");
        Assert.Contains(arena.Client.Texts, t => t == "siphon needs 1 scripture.");

        arena.Scriptures(2);
        int blood = arena.Alice.Blood;
        arena.Use("siphon");
        Assert.Equal(blood - blood / 10, arena.Alice.Blood);
        Assert.Equal(1, arena.Fx.World.Get<ItemSystem>().CountOf(arena.Alice, "scripture"));
        Assert.Equal(20, arena.Alice.Stats.Get(StatIds.Lifesteal));
        Assert.Contains(arena.Fx.World.StatusesOf(arena.Alice), s => s.Id == "siphon" && s.Magnitude == 20);
        Assert.Equal(5, arena.Fx.World.Get<SkillSystem>().Xp(arena.Alice, "religion"));

        arena.Fx.Ticks(10);
        Assert.Equal(0, arena.Alice.Stats.Get(StatIds.Lifesteal));
    }

    [Fact]
    public void AVerseNeedsAMeleeWeaponAndAFight()
    {
        var arena = new Arena(At("dummy", 6, 5));
        arena.Scriptures(5);
        arena.Use("siphon");
        Assert.Contains(arena.Client.Texts, t => t == "siphon can only be used in combat.");
        arena.Attack("dummy");
        arena.Wait(3);
        arena.Use("siphon");
        Assert.Contains(arena.Client.Texts, t => t == "siphon needs a melee weapon.");
        Assert.Equal(5, arena.Fx.World.Get<ItemSystem>().CountOf(arena.Alice, "scripture"));
    }

    [Fact]
    public void AbjurationOnlyHurtsTheOccultMade()
    {
        var arena = new Arena(At("dummy", 6, 5), At("mite_dummy", 5, 6));
        arena.Wield();
        arena.Scriptures(1);
        Npc dummy = arena.Fx.Npc("dummy");
        Npc made = arena.Fx.Npc("mite_dummy");
        arena.Attack("dummy");
        arena.Wait(3);
        arena.Use("abjure", 3);
        int before = dummy.Blood;
        arena.Use("hew", 3);
        Assert.Equal(before, dummy.Blood);

        arena.Attack("mite_dummy");
        before = made.Blood;
        arena.Use("let_blood");
        Assert.Equal(before - 400, made.Blood);
    }

    [Fact]
    public void DeathsDanceHoldsPartOfEachHitBackAsABleed()
    {
        var arena = new Arena(At("dummy", 6, 5));
        arena.Wield();
        arena.Scriptures(1);
        arena.Attack("dummy");
        arena.Wait(3);
        arena.Use("dance");
        int blood = arena.Alice.Blood;
        arena.Combat.Damage(arena.Alice, 100, arena.Fx.Npc("dummy"), HitKind.Normal, CombatStyle.Melee);
        Assert.Equal(blood - 80, arena.Alice.Blood);
        Assert.Equal(20, Assert.Single(arena.Alice.Bleeds).Remaining);
        int sent = arena.Client.Updates.Count();
        arena.Fx.World.StopInteraction(arena.Alice);
        arena.Fx.Ticks(arena.Fx.World.Rules.DeferredBleedTicks);
        Assert.Empty(arena.Alice.Bleeds);
        int bled = arena.Client.Updates.Skip(sent).SelectMany(u => u.Hits)
            .Where(h => h.TargetId == arena.Alice.Id && h.Kind == HitKind.Bleed).Sum(h => h.Amount);
        Assert.Equal(20, bled);
    }

    [Fact]
    public void RedemptionBlocksTheNextHitAndPaysPhlegmForIt()
    {
        var arena = new Arena(At("dummy", 6, 5));
        arena.Wield();
        arena.Scriptures(1);
        arena.Attack("dummy");
        arena.Wait(3);
        arena.Alice.Phlegm = 0;
        arena.Use("redeem");
        int blood = arena.Alice.Blood;
        Npc dummy = arena.Fx.Npc("dummy");
        arena.Combat.Damage(arena.Alice, blood, dummy, HitKind.Normal, CombatStyle.Melee); // a blow as big as all her blood
        Assert.Equal(blood, arena.Alice.Blood);
        Assert.Equal(50 * blood / arena.Alice.MaxBlood, arena.Alice.Phlegm);
        Assert.Equal(0, arena.Alice.Stats.Get(StatIds.BlockHit));

        arena.Combat.Damage(arena.Alice, 100, dummy, HitKind.Normal, CombatStyle.Melee);
        Assert.Equal(blood - 100, arena.Alice.Blood); // only the one
    }

    [Fact]
    public void MartyrdomScarsMakeTheNextUltimateFree()
    {
        var arena = new Arena(At("dummy", 6, 5));
        arena.Wield();
        arena.Scriptures(1);
        Npc dummy = arena.Fx.Npc("dummy");
        arena.Attack("dummy");
        arena.Wait(3);
        arena.Use("martyr", 3);
        for (int i = 0; i < 10; i++) arena.Combat.Damage(arena.Alice, 5, dummy, HitKind.Normal, CombatStyle.Melee);
        Assert.True(arena.Combat.HasFreeUltimate(arena.Alice));
        Assert.Contains(arena.Fx.World.StatusesOf(arena.Alice), s => s.Id == StatusIds.Scar && s.Magnitude == 10);

        arena.Alice.Phlegm = 0;
        int before = dummy.Blood;
        arena.Use("reap");
        Assert.Equal(before - 400, dummy.Blood);
        Assert.Equal(0, arena.Combat.Scars(arena.Alice));
    }

    [Fact]
    public void StigmataNeedsMostOfYourBloodAndPaysOffBelowThirtyPercent()
    {
        var arena = new Arena(At("dummy", 6, 5));
        arena.Wield();
        arena.Scriptures(2);
        Npc dummy = arena.Fx.Npc("dummy");
        arena.Attack("dummy");
        arena.Wait(3);
        arena.Alice.Blood = arena.Alice.MaxBlood * 8 / 10;
        arena.Use("stigmata");
        Assert.Contains(arena.Client.Texts, t => t == "stigmata needs more than 80% of your blood.");

        arena.Alice.Blood = arena.Alice.MaxBlood;
        arena.Use("stigmata", 3);
        Assert.Equal(arena.Alice.MaxBlood - arena.Alice.MaxBlood * 8 / 10, arena.Alice.Blood);
        int before = dummy.Blood;
        arena.Use("hew", 3);
        Assert.Equal(before - 130, dummy.Blood); // at 20% blood: 30% more

        arena.Alice.Blood = arena.Alice.MaxBlood / 2;
        before = dummy.Blood;
        arena.Use("let_blood");
        Assert.Equal(before - 100, dummy.Blood); // above 30%: nothing
    }

    [Fact]
    public void HemospikeFeedsBuildersAndBleedsYouForEveryAbility()
    {
        var arena = new Arena(At("dummy", 6, 5));
        arena.Wield();
        arena.Scriptures(1);
        arena.Attack("dummy");
        arena.Wait(3);
        arena.Use("spike", 3);
        arena.Alice.Phlegm = 0;
        int blood = arena.Alice.Blood;
        arena.Use("hew");
        Assert.Equal(10, arena.Alice.Phlegm);
        Assert.Equal(blood - arena.Alice.MaxBlood * 2 / 100, arena.Alice.Blood);
    }

    [Fact]
    public void TheShippedMeleeKitFightsForAThousandTicks()
    {
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig(), content, new[] { At("straw_penitent", 6, 5) });
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        ItemSystem items = fx.World.Get<ItemSystem>();
        items.Of(alice).Equipment.Set((int)EquipSlot.Weapon, "watchmans_sword", 1);
        fx.World.RecalculateStats(alice);
        items.Give(alice, "scripture_i", 50);
        Assert.Equal("strike", fx.World.Get<CombatSystem>().AutoAttack(alice).Id);

        string[] rotation = { "hew", "let_blood", "scourge", "flagellate", "sundering_blow", "exsanguinate", "red_harvest", "crimson_vow", "siphon" };
        client.FromClient(new AttackMessage { TargetId = fx.Npc("straw_penitent").Id });
        for (int i = 0; i < 1000; i++)
        {
            if (i % 3 == 0) client.FromClient(new UseAbilityMessage { AbilityId = rotation[i / 3 % rotation.Length] });
            fx.Ticks(1);
        }

        SkillSystem skills = fx.World.Get<SkillSystem>();
        Assert.True(skills.Level(alice, "melee") >= 3, "melee level " + skills.Level(alice, "melee"));
        Assert.True(skills.Xp(alice, "religion") > 0);
        Assert.True(items.CountOf(alice, "scripture_i") < 50);
        Assert.False(alice.IsDead);
    }

    [Fact]
    public void VersesCantDealDamageAndBuffsNeedAKnownStatus()
    {
        ContentParts parts = Parts();
        parts.Abilities.Add(new AbilityDef { Id = "bad", Name = "Bad", Type = AbilityType.Verse, DamageMin = 1, DamageMax = 1 });
        parts.Abilities.Add(new AbilityDef { Id = "worse", Name = "Worse", Type = AbilityType.Verse, Buff = new BuffDef { Status = "nope", Ticks = 1 } });
        var e = Assert.Throws<ContentException>(() => GameContent.Create(parts));
        Assert.Contains("'bad'", e.Message);
        Assert.Contains("'nope'", e.Message);
    }
}
