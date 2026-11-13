using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>Defence is trained by damage prevented (docs/skills.md 3.6): what wards, blocks and evasion take off a creature's blow.</summary>
public class DefenceXpTests
{
    private static WorldFixture Arena(double rate, double accuracy, string ward, int amount)
    {
        CombatRules rules = TestContent.Rules();
        rules.DefenceXpPerPrevented = rate;
        ContentParts parts = TestContent.Parts(rules);
        parts.Skills.DefenceSkill = "defence";
        // A creature that always hits for a tenth of max blood, so the numbers are exact.
        parts.Npcs.Add(new NpcDef { Id = "thumper", Name = "Thumper", Blood = 100000, Style = CombatStyle.Melee, AttackRange = 1, AttackTicks = 2, HitPercent = 10, Accuracy = accuracy, Evasion = 0, WanderRadius = 0, Aggressive = true, AggroRadius = 5 });
        parts.Items.Add(new ItemDef { Id = "charm", Name = "Charm", Equipment = new EquipmentDef { Slot = EquipSlot.Neck, Stats = { [ward] = amount } } });
        return new WorldFixture(Maps.Open(30, 30, new TileCoord(5, 5)), new ServerConfig(), GameContent.Create(parts),
            new[] { new NpcSpawn { Npc = "thumper", X = 6, Y = 5, Wander = 0 } });
    }

    private static Player Wearing(WorldFixture fx)
    {
        FakeConnection client = fx.Login("Alice");
        // Standing still, so the only Defence XP is what the defences stop (not a share of damage dealt).
        client.FromClient(new SetClientStateMessage { Key = CombatOptions.RetaliateKey, Json = "false" });
        fx.Ticks(1);
        Player alice = fx.Player("Alice");
        fx.World.Get<ItemSystem>().Of(alice).Equipment.Set((int)EquipSlot.Neck, "charm", 1);
        fx.World.RecalculateStats(alice);
        return alice;
    }

    [Fact]
    public void AWardThatTakesHalfABlowOffTeachesDefenceThatHalf()
    {
        var fx = Arena(rate: 1, accuracy: 1, StatIds.Ward, 50);
        Player alice = Wearing(fx);
        SkillSystem skills = fx.World.Get<SkillSystem>();
        double before = skills.Xp(alice, "defence");
        int blood = alice.Blood;
        fx.Ticks(4); // the thumper finds her and hits
        int taken = blood - alice.Blood;
        Assert.True(taken > 0);
        Assert.Equal(taken, skills.Xp(alice, "defence") - before, 1.0); // what was taken is what was stopped
    }

    [Fact]
    public void ABlowEvadedByArmourTeachesDefenceWhatItWouldHaveDone()
    {
        var fx = Arena(rate: 1, accuracy: 1, StatIds.Evasion, 100);
        Player alice = Wearing(fx);
        SkillSystem skills = fx.World.Get<SkillSystem>();
        double before = skills.Xp(alice, "defence");
        fx.Ticks(40);
        int blow = alice.MaxBlood / 10;
        double gained = skills.Xp(alice, "defence") - before;
        Assert.True(gained >= blow * 10, "evaded blows taught " + gained); // nearly every blow of twenty missed
    }

    [Fact]
    public void NothingWhenTheRateIsZero()
    {
        var fx = Arena(rate: 0, accuracy: 1, StatIds.Ward, 50);
        Player alice = Wearing(fx);
        double before = fx.World.Get<SkillSystem>().Xp(alice, "defence");
        fx.Ticks(10);
        Assert.Equal(before, fx.World.Get<SkillSystem>().Xp(alice, "defence"));
    }
}
