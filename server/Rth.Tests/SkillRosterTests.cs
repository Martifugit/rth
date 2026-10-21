using Newtonsoft.Json.Linq;
using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Server.Persistence;
using Rth.Shared.Combat;
using Rth.Shared.Net;
using Rth.Shared.Skills;
using Rth.Shared.World;

namespace Rth.Tests;

public class SkillRosterTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static WorldFixture World(GameContent content, params NpcSpawn[] spawns) =>
        new(Maps.Open(40, 40, Spawn), new ServerConfig(), content, spawns);

    [Fact]
    public void ARetiredSkillsXpMovesIntoItsReplacementOnLogin()
    {
        ContentParts parts = TestContent.Parts();
        parts.Skills.Retired["distillation"] = "occultism";
        var fx = World(GameContent.Create(parts));
        var save = new PlayerSave { Name = "Old", X = 5, Y = 5 };
        save.Sections["skills"] = JToken.FromObject(new Dictionary<string, double> { ["distillation"] = 500, ["occultism"] = 100 });
        fx.Store.Save("old", save);

        fx.Login("Old");
        PlayerSkills skills = fx.World.Get<SkillSystem>().Of(fx.Player("Old"));
        Assert.Equal(600, skills.Xp["occultism"]);
        Assert.False(skills.Xp.ContainsKey("distillation"));
    }

    [Theory]
    [InlineData(null, 12.5, 12.5)] // balanced by default
    [InlineData("offence", 25.0, 0.0)]
    [InlineData("defence", 0.0, 25.0)]
    public void TrainDefenceSplitsCombatXp(string? setting, double occult, double defence)
    {
        ContentParts parts = TestContent.Parts();
        parts.Skills.DefenceSkill = "defence";
        var fx = World(GameContent.Create(parts), new NpcSpawn { Npc = "dummy", X = 8, Y = 5 });
        FakeConnection client = fx.Login("Alice");
        if (setting != null) client.FromClient(new SetClientStateMessage { Key = CombatOptions.TrainDefenceKey, Json = setting });
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        fx.Ticks(1); // one auto-attack: 50 damage at 1 XP per 2 damage, so 25 XP to share

        SkillSystem skills = fx.World.Get<SkillSystem>();
        Player alice = fx.Player("Alice");
        Assert.Equal((occult, defence), (skills.Xp(alice, "occultism"), skills.Xp(alice, "defence")));
    }

    [Fact]
    public void DefenceLevelsRaiseMaxBlood()
    {
        var fx = World(TestContent.Create());
        fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Assert.Equal(1000, alice.MaxBlood);
        fx.World.Get<SkillSystem>().AddXp(alice, "defence", fx.World.Content.XpCurve.XpForLevel(11));
        Assert.Equal(1900, alice.MaxBlood); // 90 a level above 1
    }

    [Theory]
    [InlineData(1, 1, 1, 1)]
    [InlineData(100, 100, 100, 100)]
    [InlineData(99, 1, 1, 46)] // a pure occultist
    [InlineData(60, 60, 30, 54)]
    public void CombatLevelComesFromTheBestOffenceSkillDefenceAndHalfOfReligion(int occultism, int defence, int religion, int expected)
    {
        var def = new CombatLevelDef
        {
            Offence = { "melee", "ranged", "occultism" },
            Others = { ["defence"] = 1, ["religion"] = 0.5 },
        };
        var levels = new Dictionary<string, int> { ["melee"] = 1, ["ranged"] = 1, ["occultism"] = occultism, ["defence"] = defence, ["religion"] = religion };
        Assert.Equal(expected, def.Of(s => levels[s]));
    }

    [Fact]
    public void TheRealSkillsFileGivesCombatLevelsFromOneToOneHundred()
    {
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        Assert.Equal(1, content.CombatLevel.Of(_ => 1));
        Assert.Equal(100, content.CombatLevel.Of(_ => 100));
        Assert.Equal("defence", content.DefenceSkill);
        Assert.Equal("occultism", content.RetiredSkills["distillation"]);
    }
}
