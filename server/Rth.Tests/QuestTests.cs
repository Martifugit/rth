using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Skills;
using Rth.Shared.World;

namespace Rth.Tests;

public class QuestTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static QuestDef Hunt() => new()
    {
        Id = "hunt",
        Name = "The Hunt",
        Summary = "Someone wants rats dead.",
        Stages =
        {
            new QuestStage { Stage = 1, Journal = "Kill rats.", Objective = new QuestObjective { Kind = "kill", Target = "rat", Count = 2 }, Next = 2 },
            new QuestStage { Stage = 2, Journal = "Find a gem.", Objective = new QuestObjective { Kind = "item", Target = "gem" }, Next = 3 },
            new QuestStage { Stage = 3, Journal = "Report back." },
        },
        Rewards = { Xp = { new SkillXp { Skill = "might", Amount = 220 } }, Items = { new ItemCost { Item = "coins", Count = 25 } } },
        Completed = "Done hunting.",
    };

    private static WorldFixture World(QuestDef? quest = null)
    {
        ContentParts parts = TestContent.Parts();
        parts.Quests.Add(quest ?? Hunt());
        return new WorldFixture(Maps.Open(40, 40, Spawn), content: GameContent.Create(parts));
    }

    private static Npc Rat(WorldFixture fx) => new(9000, fx.World.Content.Npcs["rat"], new NpcSpawn { Npc = "rat", X = 30, Y = 30 });

    [Fact]
    public void ObjectivesAdvanceStagesAutomatically()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        QuestSystem quests = fx.World.Get<QuestSystem>();

        Assert.True(quests.Start(alice, "hunt"));
        Assert.Equal(1, quests.Stage(alice, "hunt"));
        Assert.Contains("Quest started: The Hunt.", client.Texts);

        fx.World.Events.Publish(new EntityDied(Rat(fx), alice));
        Assert.Equal(1, quests.Stage(alice, "hunt"));
        fx.Ticks(1);
        Assert.Equal("1/2", client.Sent.OfType<QuestsMessage>().Last().Quests.Single().Progress);

        fx.World.Events.Publish(new EntityDied(Rat(fx), alice));
        Assert.Equal(2, quests.Stage(alice, "hunt"));

        fx.World.Get<ItemSystem>().Give(alice, "gem", 1);
        fx.Ticks(1);
        Assert.Equal(3, quests.Stage(alice, "hunt"));
        QuestEntry entry = client.Sent.OfType<QuestsMessage>().Last().Quests.Single();
        Assert.Equal((QuestState.InProgress, "Report back."), (entry.State, entry.Journal));
    }

    [Fact]
    public void CompletingPaysRewardsOnceAndUpdatesTheJournal()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        QuestSystem quests = fx.World.Get<QuestSystem>();
        var stages = new List<QuestStageChanged>();
        fx.World.Events.Subscribe<QuestStageChanged>(stages.Add);

        quests.Start(alice, "hunt");
        quests.Complete(alice, "hunt");
        quests.Complete(alice, "hunt");
        fx.Ticks(1);

        Assert.True(quests.IsComplete(alice, "hunt"));
        Assert.Equal(25, fx.World.Get<ItemSystem>().CountOf(alice, "coins"));
        Assert.Equal(3, fx.World.Get<SkillSystem>().Level(alice, "might"));
        Assert.Single(stages, s => s.Completed);
        Assert.False(quests.Start(alice, "hunt")); // can't restart a finished quest
        QuestEntry entry = client.Sent.OfType<QuestsMessage>().Last().Quests.Single();
        Assert.Equal((QuestState.Complete, "Done hunting."), (entry.State, entry.Journal));
    }

    [Fact]
    public void UnstartedQuestsShowTheirSummary()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        fx.Ticks(1);
        QuestEntry entry = client.Sent.OfType<QuestsMessage>().Last().Quests.Single();
        Assert.Equal((QuestState.NotStarted, "Someone wants rats dead."), (entry.State, entry.Journal));
    }

    [Fact]
    public void QuestsCanRequireLevels()
    {
        QuestDef quest = Hunt();
        quest.Requirements.Add(new SkillRequirement { Skill = "butchering", Level = 4 });
        var fx = World(quest);
        FakeConnection client = fx.Login("Alice");
        Assert.False(fx.World.Get<QuestSystem>().Start(fx.Player("Alice"), "hunt"));
        Assert.Contains("You need butchering level 4 to start The Hunt.", client.Texts);
    }

    [Fact]
    public void QuestProgressIsSaved()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        QuestSystem quests = fx.World.Get<QuestSystem>();
        quests.Start(fx.Player("Alice"), "hunt");
        fx.World.Events.Publish(new EntityDied(Rat(fx), fx.Player("Alice")));
        client.FromClient(new LogoutMessage());
        fx.Ticks(1);

        fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Assert.Equal(1, quests.Stage(alice, "hunt"));
        fx.World.Events.Publish(new EntityDied(Rat(fx), alice));
        Assert.Equal(2, quests.Stage(alice, "hunt")); // the first kill was remembered
    }

    [Fact]
    public void DreamlessCanBePlayedThroughFromStartToFinish()
    {
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), content: content, spawns: new[]
        {
            new NpcSpawn { Npc = "edda_marsh", X = 8, Y = 5, Wander = 0 },
            new NpcSpawn { Npc = "corvin_sallow", X = 5, Y = 9, Wander = 0 },
        });
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        QuestSystem quests = fx.World.Get<QuestSystem>();
        ItemSystem items = fx.World.Get<ItemSystem>();
        Npc edda = fx.Npc("edda_marsh");
        Npc corvin = fx.Npc("corvin_sallow");

        DialogueMessage Line() => client.Sent.OfType<DialogueMessage>().Last();
        void Talk(Npc npc)
        {
            client.FromClient(new NpcActionMessage { NpcId = npc.Id, ActionId = "talk" });
            fx.Ticks(6);
        }
        void Say(string option)
        {
            int i = Line().Options.FindIndex(o => o == option);
            Assert.True(i >= 0, "No option '" + option + "' in: " + string.Join(" | ", Line().Options));
            client.FromClient(new DialogueChoiceMessage { Option = i });
            fx.Ticks(1);
        }
        void Next()
        {
            client.FromClient(new DialogueChoiceMessage { Option = -1 });
            fx.Ticks(1);
        }

        // Edda, asleep by the pond.
        Talk(edda);
        Assert.StartsWith("...the tide is wrong.", Line().Text);
        Next();
        Next();
        Say("What place?");
        Next();
        Say("I'll ask Corvin for you.");
        Assert.Equal(1, quests.Stage(alice, "dreamless"));

        // Corvin wants galls and a saltfig.
        Talk(corvin);
        Next(); Next(); Next(); // his introduction
        Say("Edda Marsh wants a draught to stop her dreaming.");
        Next();
        Say("Something about a grey place, and threads.");
        Assert.Equal(2, quests.Stage(alice, "dreamless"));

        items.Give(alice, "rat_gall", 3);
        fx.Ticks(1);
        Assert.Equal(3, quests.Stage(alice, "dreamless")); // the item objective noticed the galls

        Talk(corvin);
        Say("I've brought what you need for Edda's draught.");
        Assert.Equal(1, items.CountOf(alice, "dreamless_draught"));
        Assert.Equal(4, quests.Stage(alice, "dreamless"));
        int saltfigs = items.CountOf(alice, "saltfig"); // the starter kit had two; one went in the draught
        Assert.Equal(1, saltfigs);

        // Back to Edda with the draught.
        Talk(edda);
        Assert.Equal("Is that it? It smells like the pond.", Line().Text);
        Next(); Next(); Next();
        Assert.Equal("...the weaver says you left the door open.", Line().Text);
        Next();
        Assert.True(quests.IsComplete(alice, "dreamless"));
        Assert.Equal(0, items.CountOf(alice, "dreamless_draught"));
        Assert.Equal(1, items.CountOf(alice, "sleepers_ribbon"));
        Assert.Contains("Quest complete: Dreamless!", client.Texts);

        Talk(edda);
        Assert.StartsWith("I slept all night.", Line().Text);
    }
}
