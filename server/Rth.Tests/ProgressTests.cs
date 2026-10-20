using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Server.Items;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Progress;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>Bank notes and worn deposits, the achievement diary, the bestiary, client state and the quest journal.</summary>
public class ProgressTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static WorldFixture Town(GameContent? content = null, params NpcSpawn[] extra)
    {
        var spawns = new List<NpcSpawn> { new() { Npc = "banker", X = 7, Y = 5 }, new() { Npc = "merchant", X = 5, Y = 7 } };
        spawns.AddRange(extra);
        return new WorldFixture(Maps.Open(40, 40, Spawn), content: content, spawns: spawns);
    }

    private static ItemSystem Items(WorldFixture fx) => fx.World.Get<ItemSystem>();
    private static PlayerItems Alice(WorldFixture fx) => Items(fx).Of(fx.Player("Alice"));
    private static void Give(WorldFixture fx, string item, int count = 1) => Assert.Equal(0, Items(fx).Give(fx.Player("Alice"), item, count));
    private static int SlotOf(ItemContainer c, string item) => Enumerable.Range(0, c.Size).First(i => c[i].ItemId == item);

    private static FakeConnection OpenBank(WorldFixture fx)
    {
        FakeConnection client = fx.Login("Alice");
        client.FromClient(new NpcActionMessage { NpcId = fx.Npc("banker").Id, ActionId = BankSystem.Action });
        fx.Ticks(2);
        return client;
    }

    // ------------------------------------------------------------------ bank notes and worn items

    [Fact]
    public void EveryUnstackableItemHasABankNote()
    {
        GameContent content = TestContent.Create();
        ItemDef gem = content.Item("gem")!;
        ItemDef note = content.Item(gem.Note)!;
        Assert.Equal(("gem", true, "Gem"), (note.NoteOf, note.Stackable, note.Name));
        Assert.Null(content.Item("coins")!.Note); // already stacks
    }

    [Fact]
    public void WithdrawingNotedGivesOneStackAndDepositingItReturnsTheItems()
    {
        var fx = Town();
        FakeConnection client = OpenBank(fx);
        Give(fx, "gem", 20);
        client.FromClient(new BankDepositMessage { Slot = -1 });
        fx.Ticks(1);
        ItemContainer bank = fx.World.Get<BankSystem>().Of(fx.Player("Alice"));
        Assert.Equal(20, bank.CountOf("gem"));

        client.FromClient(new BankWithdrawMessage { Slot = SlotOf(bank, "gem"), Count = 15, Noted = true });
        fx.Ticks(1);
        Assert.Equal((15, 27), (Alice(fx).Inventory.CountOf("gem.note"), Alice(fx).Inventory.FreeSlots)); // one slot
        Assert.Equal(5, bank.CountOf("gem"));

        client.FromClient(new BankDepositMessage { Slot = SlotOf(Alice(fx).Inventory, "gem.note") });
        fx.Ticks(1);
        Assert.Equal((20, 0), (bank.CountOf("gem"), Alice(fx).Inventory.CountOf("gem.note")));
        Assert.Equal(0, bank.CountOf("gem.note")); // banked as the real thing
    }

    [Fact]
    public void WornItemsCanBeDepositedAndStatsFollow()
    {
        var fx = Town();
        FakeConnection client = OpenBank(fx);
        Give(fx, "hat");
        Give(fx, "sword");
        client.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = SlotOf(Alice(fx).Inventory, "hat"), ActionId = ItemActions.Equip });
        client.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = SlotOf(Alice(fx).Inventory, "sword"), ActionId = ItemActions.Equip });
        fx.Ticks(1);
        int bloodWithHat = fx.Player("Alice").MaxBlood;

        client.FromClient(new BankDepositMessage { Slot = (int)EquipSlot.Head, From = ContainerKind.Equipment });
        fx.Ticks(1);
        ItemContainer bank = fx.World.Get<BankSystem>().Of(fx.Player("Alice"));
        Assert.Equal(1, bank.CountOf("hat"));
        Assert.True(fx.Player("Alice").MaxBlood < bloodWithHat);

        client.FromClient(new BankDepositMessage { Slot = -1, From = ContainerKind.Equipment });
        fx.Ticks(1);
        Assert.Equal(1, bank.CountOf("sword"));
        Assert.True(Alice(fx).Equipment.IsEmpty((int)EquipSlot.Weapon));
    }

    // ------------------------------------------------------------------ achievements

    private static GameContent WithAchievements(params AchievementDef[] achievements)
    {
        ContentParts parts = TestContent.Parts();
        parts.Achievements = achievements.ToList();
        return GameContent.Create(parts);
    }

    [Fact]
    public void AchievementsCountEventsPayAndAnnounce()
    {
        GameContent content = WithAchievements(
            new AchievementDef { Id = "rats", Name = "Rats", Area = "Town", Objective = { Kind = "kill", Target = "rat", Count = 2 }, Reward = { Items = { new ItemCost { Item = "coins", Count = 7 } } } },
            new AchievementDef { Id = "talk", Name = "Chatty", Area = "Town", Objective = { Kind = "talk", Target = "merchant" } },
            new AchievementDef { Id = "sign", Name = "Reader", Area = "Town", Objective = { Kind = "object", Target = "sign:read" } });
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), content: content,
            spawns: new[] { new NpcSpawn { Npc = "merchant", X = 5, Y = 7 }, new NpcSpawn { Npc = "rat", X = 20, Y = 20 } },
            objects: new[] { new PlacedObject { Type = "sign", X = 6, Y = 6 } });
        FakeConnection client = fx.Login("Alice");
        AchievementSystem diary = fx.World.Get<AchievementSystem>();

        fx.World.Events.Publish(new EntityDied(fx.Npc("rat"), fx.Player("Alice")));
        Assert.False(diary.IsDone(fx.Player("Alice"), "rats"));
        fx.World.Events.Publish(new EntityDied(fx.Npc("rat"), fx.Player("Alice")));
        Assert.True(diary.IsDone(fx.Player("Alice"), "rats"));
        Assert.Equal(7, Items(fx).CountOf(fx.Player("Alice"), "coins"));

        client.FromClient(new NpcActionMessage { NpcId = fx.Npc("merchant").Id, ActionId = "talk" });
        client.FromClient(new ObjectActionMessage { ObjectType = "sign", X = 6, Y = 6, ActionId = "read" });
        fx.Ticks(4);
        Assert.True(diary.IsDone(fx.Player("Alice"), "talk"));
        Assert.True(diary.IsDone(fx.Player("Alice"), "sign"));
        Assert.Contains(client.Texts, t => t.StartsWith("Achievement unlocked: Rats"));

        AchievementProgress sent = client.Sent.OfType<JsonStateMessage>().Last(m => m.Key == AchievementSystem.StateKey).Parse<AchievementProgress>();
        Assert.Equal(new[] { "rats", "sign", "talk" }, sent.Done.OrderBy(x => x));
    }

    [Fact]
    public void LevelAndRegionAchievementsCatchUp()
    {
        GameContent content = WithAchievements(
            new AchievementDef { Id = "lvl", Name = "Learned", Objective = { Kind = "level", Target = "occultism", Count = 2 } },
            new AchievementDef { Id = "home", Name = "Home", Objective = { Kind = "region", Target = "west" } });
        var west = new MapData(0, 0, 16, 16) { Spawn = new TileCoord(3, 3) };
        var map = new WorldMap { Spawn = new TileCoord(3, 3) };
        map.AddRegion(new WorldRegion { Name = "west", X = 0, Y = 0, Width = 16, Height = 16 }, west);
        var fx = new WorldFixture(map, content: content);
        fx.Login("Alice");
        fx.Ticks(1);
        AchievementSystem diary = fx.World.Get<AchievementSystem>();
        Assert.True(diary.IsDone(fx.Player("Alice"), "home"));

        fx.World.Get<SkillSystem>().AddXp(fx.Player("Alice"), "occultism", 100000);
        Assert.True(diary.IsDone(fx.Player("Alice"), "lvl"));
    }

    [Fact]
    public void AchievementContentIsValidated()
    {
        Assert.Throws<ContentException>(() => WithAchievements(new AchievementDef { Id = "bad", Name = "Bad", Objective = { Kind = "kill", Target = "nobody" } }));
        Assert.Throws<ContentException>(() => WithAchievements(new AchievementDef { Id = "bad", Name = "Bad", Objective = { Kind = "dance", Target = "x" } }));
    }

    // ------------------------------------------------------------------ bestiary

    [Fact]
    public void TheBestiaryCountsKillsAndCreditsDropsAndCarcassYields()
    {
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), spawns: new[] { new NpcSpawn { Npc = "rat", X = 20, Y = 20 } });
        FakeConnection client = fx.Login("Alice");
        fx.World.Events.Publish(new EntityDied(fx.Npc("rat"), fx.Player("Alice")));
        fx.World.Events.Publish(new ItemsObtained(fx.Player("Alice"), "recipe:nothing", new[] { new Drop("gem", 1) }));
        fx.Ticks(1);

        BestiaryState state = client.Sent.OfType<JsonStateMessage>().Last(m => m.Key == BestiarySystem.StateKey).Parse<BestiaryState>();
        BestiaryEntry rat = state.Creatures["rat"];
        Assert.Equal(1, rat.Kills);
        Assert.Equal(5, rat.Obtained["coins"]); // the rat table always drops 5 coins
        Assert.False(rat.Obtained.ContainsKey("gem")); // an unrelated recipe isn't credited to it
    }

    // ------------------------------------------------------------------ client state

    [Fact]
    public void ClientStateIsKeptAndReturnedAtLogin()
    {
        var fx = new WorldFixture(Maps.Open(20, 20, Spawn));
        FakeConnection first = fx.Login("Alice");
        first.FromClient(new SetClientStateMessage { Key = "bars", Json = "{\"main\":[\"spark\"]}" });
        first.FromClient(new SetClientStateMessage { Key = "big", Json = new string('x', SetClientStateMessage.MaxLength + 1) });
        first.FromClient(new LogoutMessage());
        fx.Ticks(2);

        FakeConnection second = fx.Login("Alice");
        var values = second.Sent.OfType<JsonStateMessage>().Single(m => m.Key == ClientStateSystem.StateKey).Parse<Dictionary<string, string>>();
        Assert.Equal("{\"main\":[\"spark\"]}", values["bars"]);
        Assert.False(values.ContainsKey("big"));
    }

    // ------------------------------------------------------------------ quest journal

    [Fact]
    public void TheJournalTellsTheStorySoFar()
    {
        ContentParts parts = TestContent.Parts();
        parts.Quests = new List<QuestDef>
        {
            new()
            {
                Id = "tale", Name = "A Tale", Summary = "Something happened.", Completed = "And then it ended.",
                Stages = { new QuestStage { Stage = 1, Journal = "It began." }, new QuestStage { Stage = 2, Journal = "It went on." } },
                Rewards = { Items = { new ItemCost { Item = "coins", Count = 5 } } },
            },
        };
        var fx = new WorldFixture(Maps.Open(20, 20, Spawn), content: GameContent.Create(parts));
        FakeConnection client = fx.Login("Alice");
        QuestSystem quests = fx.World.Get<QuestSystem>();
        string id = "tale";
        quests.Start(fx.Player("Alice"), id);
        fx.Ticks(1);
        QuestEntry entry = client.Sent.OfType<QuestsMessage>().Last().Quests.Single(q => q.Id == id);
        Assert.Single(entry.Log);
        Assert.NotEmpty(entry.Rewards);

        quests.Complete(fx.Player("Alice"), id);
        fx.Ticks(1);
        entry = client.Sent.OfType<QuestsMessage>().Last().Quests.Single(q => q.Id == id);
        Assert.Equal(fx.World.Content.Quests[id].Stages.Count + 1, entry.Log.Count); // every stage, then the ending
    }
}
