using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Skills;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>
/// Defence's abilities (docs/skills.md 3.6): spenders of no style, so any weapon uses them, used on
/// yourself. Brace blocks part of the next blow, Steel the Flesh wards, Riposte sends part of a melee
/// blow back, Unyielding leaves you on 1 blood once when a blow would kill.
/// </summary>
public class DefenceTests
{
    private static AbilityDef Defensive(string id, BuffDef buff, int level = 1) => new()
    {
        Id = id, Name = id, Type = AbilityType.Spender, Style = CombatStyle.None, KeepsStall = true,
        CooldownTicks = 5, PhlegmRequired = 20, PhlegmCost = 10, Buff = buff,
        Requirements = level > 1 ? new List<SkillRequirement> { new() { Skill = "defence", Level = level } } : new List<SkillRequirement>(),
    };

    private sealed class Arena
    {
        public Arena()
        {
            List<AbilityDef> abilities = TestContent.Abilities();
            abilities.AddRange(new[]
            {
                Defensive("brace", new BuffDef { Status = "braced", Ticks = 10, EndsOnHitTaken = true, Stats = { [StatIds.BlockHit] = 50 } }),
                Defensive("steel", new BuffDef { Status = "steeled", Ticks = 8, Stats = { [StatIds.Ward] = 40 } }, 20),
                Defensive("riposte", new BuffDef { Status = "riposte", Ticks = 12, Stats = { [StatIds.ReflectMelee] = 50 } }),
                Defensive("unyielding", new BuffDef { Status = "unyielding", Ticks = 50, Stats = { [StatIds.SurviveLethal] = 1 } }),
            });
            ContentParts parts = TestContent.Parts(abilities: abilities);
            parts.Npcs.Add(new NpcDef { Id = "bruiser", Name = "Bruiser", Blood = 5000, Attacks = false, WanderRadius = 0 });
            foreach (string s in new[] { "braced", "steeled", "riposte", "unyielding" }) parts.Statuses.Add(new StatusDef { Id = s, Name = s });
            Fx = new WorldFixture(Maps.Open(20, 20, new TileCoord(5, 5)), new ServerConfig(), GameContent.Create(parts),
                new[] { new NpcSpawn { Npc = "bruiser", X = 6, Y = 5, Wander = 0 } });
            Client = Fx.Login("Alice");
            Alice = Fx.Player("Alice");
            Alice.Phlegm = 100;
        }

        public WorldFixture Fx { get; }
        public FakeConnection Client { get; }
        public Player Alice { get; }
        public CombatSystem Combat => Fx.World.Get<CombatSystem>();
        public Npc Brute => Fx.Npc("bruiser");

        public void Use(string ability)
        {
            Alice.LastCombatTick = Fx.World.CurrentTick;          // used on yourself, in combat
            Client.FromClient(new UseAbilityMessage { AbilityId = ability });
            Fx.Ticks(1);
        }
    }

    [Fact]
    public void BraceWorksWithAnyWeaponAndBlocksHalfTheNextBlowOnly()
    {
        var arena = new Arena();                                   // empty hands: the occult style
        arena.Use("brace");
        Assert.Equal(50, arena.Alice.Stats.Get(StatIds.BlockHit));
        Assert.Equal(90, arena.Alice.Phlegm);
        int blood = arena.Alice.Blood;
        arena.Combat.Damage(arena.Alice, 200, arena.Brute, HitKind.Normal, CombatStyle.Melee);
        Assert.Equal(blood - 100, arena.Alice.Blood);
        arena.Combat.Damage(arena.Alice, 200, arena.Brute, HitKind.Normal, CombatStyle.Melee);
        Assert.Equal(blood - 300, arena.Alice.Blood);                // the second one lands in full
    }

    [Fact]
    public void SteelTheFleshNeedsItsDefenceLevel()
    {
        var arena = new Arena();
        arena.Use("steel");
        Assert.Equal(0, arena.Alice.Stats.Get(StatIds.Ward));
        arena.Fx.World.Get<SkillSystem>().AddXp(arena.Alice, "defence", arena.Fx.World.Content.XpCurve.XpForLevel(20));
        arena.Alice.Blood = arena.Alice.MaxBlood;
        arena.Fx.Ticks(6);
        arena.Use("steel");
        Assert.Equal(40, arena.Alice.Stats.Get(StatIds.Ward));
    }

    [Fact]
    public void RiposteSendsHalfOfAMeleeBlowBack()
    {
        var arena = new Arena();
        arena.Use("riposte");
        int brute = arena.Brute.Blood;
        arena.Combat.Damage(arena.Alice, 200, arena.Brute, HitKind.Normal, CombatStyle.Melee);
        Assert.Equal(brute - 100, arena.Brute.Blood);
        arena.Combat.Damage(arena.Alice, 200, arena.Brute, HitKind.Normal, CombatStyle.Occult);   // only melee
        Assert.Equal(brute - 100, arena.Brute.Blood);
    }

    [Fact]
    public void UnyieldingLeavesOneBloodOnceThenEnds()
    {
        var arena = new Arena();
        arena.Use("unyielding");
        arena.Combat.Damage(arena.Alice, arena.Alice.Blood * 3, arena.Brute, HitKind.Normal, CombatStyle.Melee);
        Assert.False(arena.Alice.IsDead);
        Assert.Equal(1, arena.Alice.Blood);
        Assert.Equal(0, arena.Alice.Stats.Get(StatIds.SurviveLethal));
        Assert.Contains(arena.Client.Texts, t => t.Contains("You don't"));
        arena.Combat.Damage(arena.Alice, 50, arena.Brute, HitKind.Normal, CombatStyle.Melee);
        Assert.True(arena.Alice.IsDead);                             // only once
    }

    [Fact]
    public void TheShippedDefenceAbilitiesAreOfNoStyleAndNeedDefence()
    {
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        string[] ids = { "brace", "steel_the_flesh", "riposte", "unyielding" };
        int[] levels = { 1, 20, 40, 70 };
        for (int i = 0; i < ids.Length; i++)
        {
            AbilityDef a = content.Abilities[ids[i]];
            Assert.Equal(CombatStyle.None, a.Style);
            Assert.True(a.OnSelf);
            Assert.Equal(levels[i], a.Requirements.Where(r => r.Skill == "defence").Select(r => r.Level).DefaultIfEmpty(1).Single());
        }
    }
}
