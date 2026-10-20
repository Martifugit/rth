using System.Text.RegularExpressions;
using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Server.Items;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>NPC actions, dialogue, banks, shops, points, tasks and reagents.</summary>
public class TownTests
{
    private static readonly TileCoord Spawn = new(5, 5);

    private static WorldFixture Town(params NpcSpawn[] extra)
    {
        var spawns = new List<NpcSpawn>
        {
            new() { Npc = "banker", X = 7, Y = 5 },
            new() { Npc = "merchant", X = 5, Y = 7 },
        };
        spawns.AddRange(extra);
        return new WorldFixture(Maps.Open(40, 40, Spawn), spawns: spawns);
    }

    private static Npc NpcOf(WorldFixture fx, string id) => fx.Npc(id);
    private static ItemSystem Items(WorldFixture fx) => fx.World.Get<ItemSystem>();
    private static ItemContainer Pack(WorldFixture fx) => Items(fx).Of(fx.Player("Alice")).Inventory;
    private static int Count(WorldFixture fx, string item) => Items(fx).CountOf(fx.Player("Alice"), item);
    private static void Give(WorldFixture fx, string item, int count = 1) => Assert.Equal(0, Items(fx).Give(fx.Player("Alice"), item, count));
    private static int SlotOf(ItemContainer c, string item) => Enumerable.Range(0, c.Size).First(i => c[i].ItemId == item);

    private static DialogueMessage LastLine(FakeConnection c) => c.Sent.OfType<DialogueMessage>().Last();

    private static void Talk(FakeConnection client, Npc npc, string action = "talk") =>
        client.FromClient(new NpcActionMessage { NpcId = npc.Id, ActionId = action });

    private static void Choose(WorldFixture fx, FakeConnection client, string optionText)
    {
        int index = LastLine(client).Options.FindIndex(o => o == optionText);
        Assert.True(index >= 0, "No option '" + optionText + "' in: " + string.Join(" | ", LastLine(client).Options));
        client.FromClient(new DialogueChoiceMessage { Option = index });
        fx.Ticks(1);
    }

    private static void Continue(WorldFixture fx, FakeConnection client)
    {
        client.FromClient(new DialogueChoiceMessage { Option = -1 });
        fx.Ticks(1);
    }

    // ------------------------------------------------------------------ NPC actions and dialogue

    [Fact]
    public void TalkingWalksOverThenRunsTheConversation()
    {
        var fx = Town();
        FakeConnection client = fx.Login("Alice");
        Talk(client, NpcOf(fx, "merchant"));
        fx.Ticks(2);

        Assert.True(fx.World.IsBeside(fx.Player("Alice"), 0, NpcOf(fx, "merchant").Position));
        DialogueMessage intro = LastLine(client);
        Assert.Equal(("Merchant", "Welcome, Alice."), (intro.Speaker, intro.Text));
        Assert.Empty(intro.Options);

        Continue(fx, client);
        DialogueMessage greet = LastLine(client);
        Assert.Equal("What'll it be? You have 0 tokens.", greet.Text);
        Assert.Equal(new[] { "Trade", "Work", "Bank", "Bye" }, greet.Options); // "Rich option" is hidden

        Choose(fx, client, "Bye");
        Assert.False(LastLine(client).Open);
    }

    [Fact]
    public void FlagsRememberEarlierConversations()
    {
        var fx = Town();
        FakeConnection client = fx.Login("Alice");
        Talk(client, NpcOf(fx, "merchant"));
        fx.Ticks(2);
        Continue(fx, client);
        Choose(fx, client, "Bye");

        Talk(client, NpcOf(fx, "merchant"));
        fx.Ticks(1);
        Assert.StartsWith("What'll it be?", LastLine(client).Text); // straight past the introduction
        Assert.Equal("1", fx.World.Get<FlagSystem>().Get(fx.Player("Alice"), "met"));
    }

    [Fact]
    public void OptionsWithMetConditionsAppearAndTheirActionsRun()
    {
        var fx = Town();
        FakeConnection client = fx.Login("Alice");
        fx.World.Get<FlagSystem>().Set(fx.Player("Alice"), "met", "1");
        Give(fx, "coins", 150);
        Talk(client, NpcOf(fx, "merchant"));
        fx.Ticks(2);

        Choose(fx, client, "Rich option");
        Assert.Equal("Ooh.", LastLine(client).Text);
        Assert.Equal(1, Count(fx, "gem"));
    }

    [Fact]
    public void WalkingAwayEndsTheConversation()
    {
        var fx = Town();
        FakeConnection client = fx.Login("Alice");
        Talk(client, NpcOf(fx, "merchant"));
        fx.Ticks(2);
        client.FromClient(new WalkToMessage { X = 20, Y = 20 });
        fx.Ticks(4);
        Assert.False(LastLine(client).Open);
        Assert.False(fx.World.Get<DialogueSystem>().State(fx.Player("Alice")).Active);
    }

    [Fact]
    public void NpcsHoldStillWhileTalkedTo()
    {
        var fx = Town(new NpcSpawn { Npc = "rat", X = 20, Y = 20 });
        Npc merchant = NpcOf(fx, "merchant");
        FakeConnection client = fx.Login("Alice");
        Talk(client, merchant);
        fx.Ticks(2);
        Assert.True(merchant.BusyUntilTick > fx.World.CurrentTick);
    }

    [Fact]
    public void LockedActionsExplainThemselves()
    {
        var fx = Town();
        FakeConnection client = fx.Login("Alice");
        Talk(client, NpcOf(fx, "merchant"), "secret");
        fx.Ticks(1);
        Assert.Contains("Not yet.", client.Texts);
        Assert.Null(fx.Player("Alice").Interaction);
    }

    [Fact]
    public void TownsfolkCantBeAttacked()
    {
        var fx = Town();
        FakeConnection client = fx.Login("Alice");
        client.FromClient(new AttackMessage { TargetId = NpcOf(fx, "banker").Id });
        fx.Ticks(1);
        Assert.Equal(0, fx.Player("Alice").TargetId);
    }

    [Fact]
    public void EveryShippedDialogueUsesOnlyKnownConditionsActionsAndTokens()
    {
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        var fx = new WorldFixture(Maps.Open(10, 10, new TileCoord(1, 1)), content: content);
        DialogueSystem dialogue = fx.World.Get<DialogueSystem>();
        var conditions = dialogue.ConditionTypes.ToHashSet();
        var actions = dialogue.ActionTypes.ToHashSet();
        var tokens = dialogue.TokenNames.ToHashSet();
        var token = new Regex(@"\{([a-zA-Z]+)(?::[a-zA-Z0-9_]+)?\}");

        foreach (DialogueDef d in content.Dialogues.Values)
        {
            foreach ((string id, DialogueNode node) in d.Nodes)
            {
                string where = d.Id + "/" + id;
                var conds = node.Branches.SelectMany(b => b.Conditions).Concat(node.Options.SelectMany(o => o.Conditions));
                var acts = node.Actions.Concat(node.Options.SelectMany(o => o.Actions));
                foreach (DialogueCondition c in conds) Assert.True(conditions.Contains(c.Type), where + ": no handler for condition '" + c.Type + "'");
                foreach (DialogueAction a in acts) Assert.True(actions.Contains(a.Type), where + ": no handler for action '" + a.Type + "'");
                foreach (string text in node.Options.Select(o => o.Text).Append(node.Text))
                    foreach (Match m in token.Matches(text))
                        Assert.True(tokens.Contains(m.Groups[1].Value), where + ": no provider for token {" + m.Groups[1].Value + "}");
            }
        }
    }

    // ------------------------------------------------------------------ bank

    private static FakeConnection OpenBank(WorldFixture fx)
    {
        FakeConnection client = fx.Login("Alice");
        Talk(client, NpcOf(fx, "banker"), BankSystem.Action);
        fx.Ticks(2);
        Assert.Equal(InterfaceKind.Bank, client.Sent.OfType<InterfaceMessage>().Last().Kind);
        return client;
    }

    [Fact]
    public void TheBankStacksEverythingAndGivesItBack()
    {
        var fx = Town();
        FakeConnection client = OpenBank(fx);
        Give(fx, "bread", 3);
        Give(fx, "coins", 40);

        client.FromClient(new BankDepositMessage { Slot = SlotOf(Pack(fx), "bread"), Count = 0 }); // all 3
        client.FromClient(new BankDepositMessage { Slot = SlotOf(Pack(fx), "coins"), Count = 15 });
        fx.Ticks(1);

        ItemContainer bank = fx.World.Get<BankSystem>().Of(fx.Player("Alice"));
        Assert.Equal((3, 15, 25), (bank.CountOf("bread"), bank.CountOf("coins"), Count(fx, "coins")));
        Assert.Equal(1, Enumerable.Range(0, bank.Size).Count(i => bank[i].ItemId == "bread")); // one stack
        Assert.Contains(client.Sent.OfType<ContainerMessage>(), m => m.Kind == ContainerKind.Bank);

        client.FromClient(new BankWithdrawMessage { Slot = 0, Count = 2 });
        fx.Ticks(1);
        Assert.Equal((1, 2), (bank.CountOf("bread"), Count(fx, "bread"))); // non-stackables come out one per slot
    }

    [Fact]
    public void DepositAllEmptiesThePackAndWithdrawStopsWhenFull()
    {
        var fx = Town();
        FakeConnection client = OpenBank(fx);
        Give(fx, "gem", 28);
        client.FromClient(new BankDepositMessage { Slot = -1 });
        fx.Ticks(1);
        Assert.Equal(28, Pack(fx).FreeSlots);

        Give(fx, "coins", 1);
        Give(fx, "bread", 25);
        client.FromClient(new BankWithdrawMessage { Slot = 0, Count = 0 });
        fx.Ticks(1);
        Assert.Equal(2, Count(fx, "gem"));
        Assert.Equal(26, fx.World.Get<BankSystem>().Of(fx.Player("Alice")).CountOf("gem"));
    }

    [Fact]
    public void BankingOnlyWorksWithTheBankOpenAndItClosesWhenYouLeave()
    {
        var fx = Town();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "gem");
        client.FromClient(new BankDepositMessage { Slot = -1 });
        fx.Ticks(1);
        Assert.Equal(1, Count(fx, "gem"));

        Talk(client, NpcOf(fx, "banker"), BankSystem.Action);
        fx.Ticks(2);
        client.FromClient(new WalkToMessage { X = 20, Y = 5 });
        fx.Ticks(5);
        Assert.Equal(InterfaceKind.None, client.Sent.OfType<InterfaceMessage>().Last().Kind);
        client.FromClient(new BankDepositMessage { Slot = -1 });
        fx.Ticks(1);
        Assert.Equal(1, Count(fx, "gem"));
    }

    [Fact]
    public void TheBankIsSaved()
    {
        var fx = Town();
        FakeConnection client = OpenBank(fx);
        Give(fx, "gem", 3);
        client.FromClient(new BankDepositMessage { Slot = -1 });
        client.FromClient(new LogoutMessage());
        fx.Ticks(1);

        fx.Login("Alice");
        Assert.Equal(3, fx.World.Get<BankSystem>().Of(fx.Player("Alice")).CountOf("gem"));
    }

    [Fact]
    public void DialogueCanOpenTheBank()
    {
        var fx = Town();
        FakeConnection client = fx.Login("Alice");
        fx.World.Get<FlagSystem>().Set(fx.Player("Alice"), "met", "1");
        Talk(client, NpcOf(fx, "merchant"));
        fx.Ticks(2);
        Choose(fx, client, "Bank");
        Assert.Equal(InterfaceKind.Bank, client.Sent.OfType<InterfaceMessage>().Last().Kind);
        Assert.False(LastLine(client).Open); // the window replaces the conversation
    }

    // ------------------------------------------------------------------ shops

    private static FakeConnection OpenShop(WorldFixture fx)
    {
        FakeConnection client = fx.Login("Alice");
        Talk(client, NpcOf(fx, "merchant"), ShopSystem.Trade);
        fx.Ticks(2);
        Assert.Equal(InterfaceKind.Shop, client.Sent.OfType<InterfaceMessage>().Last().Kind);
        return client;
    }

    [Fact]
    public void BuyingSpendsTheCurrencyAndLimitedStockRunsOutThenRestocks()
    {
        var fx = Town();
        FakeConnection client = OpenShop(fx);
        Give(fx, "coins", 100);

        client.FromClient(new ShopBuyMessage { Index = 0, Count = 5 }); // only 2 in stock
        fx.Ticks(1);
        Assert.Equal((2, 90), (Count(fx, "bread"), Count(fx, "coins")));
        Assert.Equal(0, client.Sent.OfType<ShopMessage>().Last().Entries[0].Stock);

        client.FromClient(new ShopBuyMessage { Index = 0 });
        fx.Ticks(1);
        Assert.Contains("That's sold out.", client.Texts);

        fx.Ticks(10);
        Assert.Equal(1, client.Sent.OfType<ShopMessage>().Last().Entries[0].Stock);
    }

    [Fact]
    public void YouCantBuyWhatYouCantAfford()
    {
        var fx = Town();
        FakeConnection client = OpenShop(fx);
        Give(fx, "coins", 40);
        client.FromClient(new ShopBuyMessage { Index = 1 }); // gem: 50
        fx.Ticks(1);
        Assert.Contains("You can't afford that.", client.Texts);
        Assert.Equal(40, Count(fx, "coins"));
    }

    [Fact]
    public void SellingPaysAShareOfTheValue()
    {
        var fx = Town();
        FakeConnection client = OpenShop(fx);
        fx.World.QueueContent(WithGemValue(80));
        fx.Ticks(1);
        Give(fx, "gem");
        client.FromClient(new ShopSellMessage { Slot = SlotOf(Pack(fx), "gem") });
        fx.Ticks(1);
        Assert.Equal((0, 40), (Count(fx, "gem"), Count(fx, "coins"))); // 50% of 80

        static GameContent WithGemValue(int value)
        {
            ContentParts parts = TestContent.Parts();
            parts.Items.Single(i => i.Id == "gem").Value = value;
            return GameContent.Create(parts);
        }
    }

    [Fact]
    public void PointsShopsSpendPoints()
    {
        var fx = Town();
        FakeConnection client = fx.Login("Alice");
        fx.World.Get<PointsSystem>().Add(fx.Player("Alice"), "tokens", 12);
        fx.World.Get<ShopSystem>().Open(fx.Player("Alice"), "token_shop", null);
        client.FromClient(new ShopBuyMessage { Index = 0 });
        fx.Ticks(1);
        Assert.Equal(1, Count(fx, "sword"));
        Assert.Equal(2, fx.World.Get<PointsSystem>().Balance(fx.Player("Alice"), "tokens"));
        Assert.Equal("Tokens", client.Sent.OfType<ShopMessage>().Last().CurrencyName);
    }

    // ------------------------------------------------------------------ tasks and points

    [Fact]
    public void TasksAreAssignedInDialogueTrackedByEventsAndPaidInPoints()
    {
        var fx = Town(new NpcSpawn { Npc = "rat", X = 9, Y = 7 }, new NpcSpawn { Npc = "rat", X = 9, Y = 9 });
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        fx.World.Get<FlagSystem>().Set(alice, "met", "1");

        Talk(client, NpcOf(fx, "merchant"));
        fx.Ticks(2);
        Choose(fx, client, "Work");
        Assert.Equal("Go: Kill 2 rats. (0/2)", LastLine(client).Text);
        Assert.Equal("Kill 2 rats. (0/2)", client.Sent.OfType<TaskMessage>().Last().Description);

        CombatSystem combat = fx.World.Get<CombatSystem>();
        foreach (Npc rat in fx.World.Npcs.Where(n => n.Def.Id == "rat").ToList())
            combat.Damage(rat, 10_000, alice, HitKind.Normal);
        fx.Ticks(1);

        Assert.False(fx.World.Get<TaskSystem>().Of(alice).Active);
        Assert.Equal(5, fx.World.Get<PointsSystem>().Balance(alice, "tokens"));
        Assert.Contains(client.Texts, t => t.StartsWith("Task complete"));
        Assert.Equal(5, client.Sent.OfType<TaskMessage>().Last().Points["Tokens"]);
    }

    [Fact]
    public void StreaksPayABonus()
    {
        var fx = Town();
        fx.Login("Alice");
        Player alice = fx.Player("Alice");
        TaskSystem tasks = fx.World.Get<TaskSystem>();
        var rat = new Npc(9999, fx.World.Content.Npcs["rat"], new NpcSpawn { Npc = "rat", X = 30, Y = 30 });
        for (int i = 0; i < 3; i++)
        {
            Assert.True(tasks.Assign(alice, "boss"));
            fx.World.Events.Publish(new EntityDied(rat, alice));
            fx.World.Events.Publish(new EntityDied(rat, alice));
        }
        Assert.Equal(5 + 5 + 10, fx.World.Get<PointsSystem>().Balance(alice, "tokens")); // every 3rd is doubled
    }

    [Fact]
    public void GiversCanRequireLevelsAndTasksCanBeDroppedForPoints()
    {
        var fx = Town();
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        TaskSystem tasks = fx.World.Get<TaskSystem>();

        Assert.Equal("You need butchering level 5 to take tasks from Elite.", tasks.WhyNot(alice, "elite"));
        Assert.True(tasks.Assign(alice, "boss"));
        Assert.Equal("You already have a task.", tasks.WhyNot(alice, "boss"));

        tasks.Cancel(alice);
        Assert.True(tasks.Of(alice).Active); // can't afford the 2 points
        fx.World.Get<PointsSystem>().Add(alice, "tokens", 2);
        tasks.Cancel(alice);
        Assert.False(tasks.Of(alice).Active);
        Assert.Equal(0, fx.World.Get<PointsSystem>().Balance(alice, "tokens"));
    }

    [Fact]
    public void TasksAndPointsAreSaved()
    {
        var fx = Town();
        FakeConnection client = fx.Login("Alice");
        fx.World.Get<TaskSystem>().Assign(fx.Player("Alice"), "boss");
        fx.World.Get<PointsSystem>().Add(fx.Player("Alice"), "tokens", 7);
        client.FromClient(new LogoutMessage());
        fx.Ticks(1);

        fx.Login("Alice");
        Assert.True(fx.World.Get<TaskSystem>().Of(fx.Player("Alice")).Active);
        Assert.Equal(7, fx.World.Get<PointsSystem>().Balance(fx.Player("Alice"), "tokens"));
    }

    // ------------------------------------------------------------------ reagents

    [Fact]
    public void ReagentsAreRequiredAndUsedUp()
    {
        List<AbilityDef> abilities = TestContent.Abilities();
        abilities.Single(a => a.Id == "spark").Reagents.Add(new ItemCost { Item = "gem", Count = 2 });
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), content: TestContent.Create(abilities: abilities),
            spawns: new[] { new NpcSpawn { Npc = "dummy", X = 8, Y = 5 } });
        FakeConnection client = fx.Login("Alice");
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(1);
        Assert.Contains("Spark needs 2 gem.", client.Texts);

        Give(fx, "gem", 3);
        client.FromClient(new UseAbilityMessage { AbilityId = "spark" });
        fx.Ticks(4);
        Assert.Equal(1, Count(fx, "gem"));
    }

    // ------------------------------------------------------------------ the real town, end to end

    [Fact]
    public void CorvinOffersHisStillToABlooodiedButcherAndTheStillMakesTincture()
    {
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        MapData map = Maps.Open(40, 40, Spawn);
        map.Block(0, 10, 10);
        var fx = new WorldFixture(map, content: content,
            spawns: new[] { new NpcSpawn { Npc = "corvin_sallow", X = 8, Y = 5, Wander = 0 } },
            objects: new[] { new PlacedObject { Type = "apothecary_still", X = 10, Y = 10 } });
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        Give(fx, "rat_gall", 2);
        int tinctureBefore = Count(fx, "melancholic_tincture"); // the starter kit has some

        // The still is locked until Corvin offers it.
        client.FromClient(new ObjectActionMessage { ObjectType = "apothecary_still", X = 10, Y = 10, ActionId = "process" });
        fx.Ticks(1);
        Assert.Contains(client.Texts, t => t.Contains("ask him about it"));

        Npc corvin = fx.Npc("corvin_sallow");
        Talk(client, corvin);
        fx.Ticks(3);
        Assert.StartsWith("Ah! You must be the one who woke up.", LastLine(client).Text);
        Continue(fx, client);
        Continue(fx, client);
        Continue(fx, client);
        Choose(fx, client, "You said something about the humours...");
        Choose(fx, client, "Handled how?");
        Assert.StartsWith("Ha! Get some blood on your hands first.", LastLine(client).Text);

        fx.World.Get<SkillSystem>().AddXp(alice, "butchering", content.XpCurve.XpForLevel(5));
        Talk(client, corvin);
        fx.Ticks(1);
        Choose(fx, client, "You said something about the humours...");
        Choose(fx, client, "Handled how?");
        Choose(fx, client, "Medicine it is.");
        Assert.StartsWith("Use it whenever you like.", LastLine(client).Text);

        client.FromClient(new ObjectActionMessage { ObjectType = "apothecary_still", X = 10, Y = 10, ActionId = "process" });
        fx.Ticks(15);
        Assert.Equal(tinctureBefore + 6, Count(fx, "melancholic_tincture")); // 3 per gall
        Assert.Equal(0, Count(fx, "rat_gall"));
        Assert.True(fx.World.Get<SkillSystem>().Xp(alice, "distillation") >= 20);
    }
}
