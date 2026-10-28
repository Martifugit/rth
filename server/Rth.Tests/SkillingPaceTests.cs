using Rth.Server.Content;
using Rth.Shared.Items;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>
/// The shipped skilling content against the pace table (docs/skills.md 2.1): the XP an hour of
/// working without a break, at the content's own level, falls in its band's range. The best
/// ordinary method (worked actively) aims at the target; AFK at about half of it.
/// </summary>
public class SkillingPaceTests
{
    private static readonly GameContent Content = GameContent.Load(Repo.ContentDirectory);

    /// <summary>The pace table's XP an hour for a level's band.</summary>
    private static double Target(int level) => level switch
    {
        < 10 => 4000, < 20 => 5000, < 30 => 6000, < 40 => 10000, < 50 => 18000, < 60 => 38000, < 70 => 77000, < 80 => 155000, < 90 => 335000, _ => 700000,
    };

    private static double PerHour(int ticks) => 3600 / (ticks * 0.6);

    [Theory]
    [InlineData("yarrow_patch")]
    [InlineData("rue_patch")]
    [InlineData("feverfew_patch")]
    [InlineData("reed_bed")]
    [InlineData("flax_patch")]
    [InlineData("berry_bush")]
    public void GatheringNodesKeepToTheTable(string id) =>
        Assert.InRange(AfkXpPerHour(id), Target(Content.Objects[id].Node!.Level) * 0.8, Target(Content.Objects[id].Node!.Level) * 1.5);

    /// <summary>
    /// Mining and fishing are slow when left to themselves: AFK they give about half the table, and
    /// their active layers (fire-setting; fishing's, to come) are what bring them up to it.
    /// </summary>
    [Theory]
    [InlineData("copper_seam")]
    [InlineData("ironstone_seam")]
    [InlineData("hematite_seam")]
    [InlineData("river_spot")]
    public void MiningAndFishingAreSlowWhenLeftAlone(string id) =>
        Assert.InRange(AfkXpPerHour(id), Target(Content.Objects[id].Node!.Level) * 0.4, Target(Content.Objects[id].Node!.Level) * 0.65);

    /// <summary>The XP an hour at a node's own level with the best shipped tool of its tier, nothing active.</summary>
    private static double AfkXpPerHour(string id)
    {
        NodeDef def = Content.Objects[id].Node!;
        ToolDef? tool = def.Tool == null ? null : Content.Items.Values.Select(i => i.Tool).Where(t => t != null && t.Kind == def.Tool
                && t.Requirements.All(r => r.Level <= def.Level)).OrderByDescending(t => t!.Tier).FirstOrDefault();
        double perAttempt;
        if (def.Progress != null)
            perAttempt = def.Progress.StrikeXp + def.Xp * Math.Min(1, def.Progress.PerStrike * (1 + (tool?.Success ?? 0)) / def.Progress.Needed);
        else
        {
            // Catches at the node's own level: the ones the player can get, by weight.
            List<NodeCatch> can = def.Catches.Where(c => c.Level <= def.Level).ToList();
            double xp = can.Count > 0 ? can.Sum(c => c.Xp * c.Weight) / can.Sum(c => c.Weight) : def.Xp;
            perAttempt = Math.Min(def.MaxChance, def.BaseChance + (tool?.Success ?? 0)) * xp;
        }
        return PerHour(def.Ticks) * perAttempt;
    }

    [Fact]
    public void PikeAtItsLevelKeepsTheRiverSpotInItsBand()
    {
        NodeDef def = Content.Objects["river_spot"].Node!;
        double xp = def.Catches.Average(c => c.Xp);              // both perch and pike at level 20
        double xpPerHour = PerHour(def.Ticks) * Math.Min(def.MaxChance, def.BaseChance + def.PerLevel * (20 - def.Level)) * xp;
        Assert.InRange(xpPerHour, Target(20) * 0.4, Target(20) * 0.65);     // AFK: half the table
    }

    [Fact]
    public void FourEelWeirsGiveAtLeastHalfTheBandWhileYouDoOtherThings()
    {
        NodeDef def = Content.Objects["eel_weir"].Node!;
        double catchesPerHour = PerHour(def.Trap!.Ticks) * Math.Min(def.MaxChance, def.BaseChance);
        Assert.InRange(4 * catchesPerHour * def.Xp, Target(def.Level) * 0.5, Target(def.Level) * 1.0);
    }

    /// <summary>
    /// Every smithing recipe: AFK at the forge's base speed is about half the table or a little
    /// more; with the draw kept roaring it is the table, up to a third over for the big pieces.
    /// </summary>
    [Fact]
    public void SmithingKeepsToTheTableAfkAndHot()
    {
        double hot = Content.Objects["forge"].Station!.Draw!.HotTickScale;
        foreach (RecipeDef r in Content.Recipes.Values.Where(r => r.Station == "forge"))
        {
            double afk = PerHour(r.Ticks) * r.Xp, roaring = PerHour(r.Ticks) / hot * r.Xp;
            double target = Target(r.Level);
            Assert.True(afk >= target * 0.4 && afk <= target * 0.9, r.Id + " AFK " + afk);
            Assert.True(roaring >= target * 0.8 && roaring <= target * 1.5, r.Id + " hot " + roaring);
        }
    }

    [Fact]
    public void CookingAtTheRangeKeepsToTheTableAtTheFoodsLevel()
    {
        double fail = Content.Objects["range"].Station!.FailScale;
        foreach (RecipeDef r in Content.Recipes.Values.Where(r => r.Station == "range"))
        {
            double xpPerHour = PerHour(r.Ticks) * r.Xp * (1 - r.FailChance(r.Level) * fail);
            Assert.True(xpPerHour >= Target(r.Level) * 0.7 && xpPerHour <= Target(r.Level) * 1.5, r.Id + " " + xpPerHour);
        }
    }

    [Fact]
    public void PotionsKeepToTheTable()
    {
        foreach (RecipeDef r in Content.Recipes.Values.Where(r => r.Id.StartsWith("brew_")))
        {
            double xpPerHour = PerHour(r.Ticks) * r.Xp;
            Assert.True(xpPerHour >= Target(r.Level) * 0.8 && xpPerHour <= Target(r.Level) * 1.5, r.Id + " " + xpPerHour);
        }
    }

    [Fact]
    public void EveryNewSkillHasSomethingToDoAtLevelOne()
    {
        foreach (string skill in new[] { "smithing", "cooking", "fishing", "foraging", "medicine" })
        {
            bool node = Content.Objects.Values.Any(o => o.Node?.Skill == skill && o.Node.Level == 1);
            bool recipe = Content.Recipes.Values.Any(r => r.Skill == skill && r.Level == 1);
            Assert.True(node || recipe, skill + " has nothing at level 1");
        }
    }
}
