using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Progress;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>
/// D3, the long way back: trials (a timed run through regions with a drain and once-per-attempt
/// lanterns), tile triggers (grasping hands), NPC patrols and hits by a share of max blood, and the
/// walker giving the ferryman's hold back for nothing.
/// </summary>
public class TrialTests
{
    // The landing is x 0-15, the way x 16-47; both 16 tall.
    private static readonly TileCoord Spawn = new(3, 3);
    private static readonly TileCoord Wake = new(5, 5);
    private static readonly TileCoord Gate = new(8, 8);
    private static readonly TileCoord Start = new(18, 8);
    private static readonly TileCoord Lantern = new(22, 8);
    private static readonly TileCoord Hands = new(30, 8);

    private static WorldMap TwoRegions()
    {
        var landing = new MapData(0, 0, 16, 16) { Spawn = Spawn };
        var way = new MapData(16, 0, 32, 16);
        var world = new WorldMap { Spawn = Spawn };
        world.AddRegion(new WorldRegion { Name = "landing", Scene = "landing", X = 0, Y = 0, Width = 16, Height = 16, HasSpawn = true }, landing);
        world.AddRegion(new WorldRegion { Name = "way", Scene = "way", X = 16, Y = 0, Width = 32, Height = 16 }, way);
        return world;
    }

    private static WorldFixture Field(Action<ContentParts>? tweak = null, IEnumerable<NpcSpawn>? spawns = null)
    {
        ContentParts parts = TestContent.Parts();
        parts.Items.First(i => i.Id == "sword").Value = 1000;
        parts.Death.Wake = new TilePlace { X = Wake.X, Y = Wake.Y };
        parts.Statuses.Add(new StatusDef { Id = "cold", Name = "The cold", Debuff = true });
        parts.Objects.Add(new ObjectDef { Id = "soul_gate", Name = "Gate", Actions = { new ActionDef { Id = "trial", Label = "Walk" } } });
        parts.Objects.Add(new ObjectDef
        {
            Id = "soul_lantern", Name = "Lantern",
            Actions = { new ActionDef { Id = "kindle", Label = "Kindle", OncePer = "trial", UsedMessage = "spent", Effects = { new EffectOp { Op = EffectOp.HealBloodPercent, Amount = 30 } } } },
        });
        parts.Objects.Add(new ObjectDef { Id = "hands", Name = "Hands", Trigger = new TriggerDef { Hidden = true, HoldTicks = 3, DamagePercent = 10, RearmTicks = 20, Message = "grabbed" } });
        parts.Trials.Add(new TrialDef
        {
            Id = "long_way", Title = "The long way back", Regions = { "way" }, Gate = "soul_gate",
            Start = new TilePlace { X = Start.X, Y = Start.Y },
            Conditions = { new DialogueCondition { Type = "ferryHolds" } }, LockedMessage = "nothing to walk for",
            EmptyHanded = true, EmptyHandedMessage = "empty-handed",
            Drain = new TrialDrain { Percent = 1, EveryTicks = 2 }, Status = "cold",
            FailMessage = "lost", WinMessage = "made it in {time}",
        });
        parts.Npcs.Add(new NpcDef { Id = "shade", Name = "Shade", Blood = 100, Attackable = false, Aggressive = true, AggroRadius = 2, LeashRadius = 6, AttackTicks = 4, HitPercent = 20, Accuracy = 1.0, WanderRadius = 0 });
        parts.Dialogues.Add(new DialogueDef
        {
            Id = "walker",
            Nodes =
            {
                ["start"] = new DialogueNode
                {
                    Text = "You made it.",
                    Options = { new DialogueOption { Text = "Thank you", Actions = { new DialogueAction { Type = "trialWin", Id = "long_way" }, new DialogueAction { Type = "ferryReturnFree" } } } },
                },
            },
        });
        tweak?.Invoke(parts);
        var objects = new[]
        {
            new PlacedObject { Type = "soul_gate", X = Gate.X, Y = Gate.Y },
            new PlacedObject { Type = "soul_lantern", X = Lantern.X, Y = Lantern.Y },
            new PlacedObject { Type = "hands", X = Hands.X, Y = Hands.Y },
        };
        return new WorldFixture(TwoRegions(), content: GameContent.Create(parts), spawns: spawns, objects: objects);
    }

    private static TrialSystem Trials(WorldFixture fx) => fx.World.Get<TrialSystem>();
    private static Player Alice(WorldFixture fx) => fx.Player("Alice");

    /// <summary>Alice dies with a sword and wakes on the landing, empty-handed, the sword held.</summary>
    private static FakeConnection DeadAndAwake(WorldFixture fx)
    {
        FakeConnection client = fx.Login("Alice");
        fx.World.Get<ItemSystem>().Of(Alice(fx)).Equipment.Set((int)EquipSlot.Weapon, "sword", 1);
        fx.World.Get<CombatSystem>().Damage(Alice(fx), 1_000_000, null, HitKind.Normal);
        fx.Ticks(fx.World.Rules.RespawnDelayTicks + 2);
        Assert.False(Alice(fx).IsDead);
        Assert.Equal(Wake, Alice(fx).Position);
        fx.World.Get<DialogueSystem>().End(Alice(fx));
        return client;
    }

    private static void Use(FakeConnection client, WorldFixture fx, string type, TileCoord at, string action)
    {
        client.FromClient(new ObjectActionMessage { ObjectType = type, X = at.X, Y = at.Y, ActionId = action });
        fx.Ticks(12);
    }

    // ------------------------------------------------------------------ beginning

    [Fact]
    public void NothingHeldMeansNothingToWalkFor()
    {
        WorldFixture fx = Field();
        FakeConnection client = fx.Login("Alice");
        Use(client, fx, "soul_gate", Gate, "trial");
        Assert.False(Trials(fx).Running(Alice(fx)));
        Assert.Contains("nothing to walk for", client.Texts);
    }

    [Fact]
    public void TheGateTakesTheDeadToTheStartOfTheWay()
    {
        WorldFixture fx = Field();
        FakeConnection client = DeadAndAwake(fx);
        Use(client, fx, "soul_gate", Gate, "trial");
        Assert.True(Trials(fx).Running(Alice(fx), "long_way"));
        Assert.InRange(Alice(fx).Position.ChebyshevDistance(Start), 0, 0);
        Assert.Contains(client.Sent.OfType<JsonStateMessage>(), m => m.Key == TrialSystem.StateKey && m.Json.Contains("long_way"));
        Assert.Contains(fx.World.StatusesOf(Alice(fx)), s => s.Id == "cold");
    }

    [Fact]
    public void YouGoEmptyHanded()
    {
        WorldFixture fx = Field();
        FakeConnection client = DeadAndAwake(fx);
        Assert.Equal(0, fx.World.Get<ItemSystem>().Give(Alice(fx), "bread", 1));
        Assert.False(Trials(fx).Begin(Alice(fx), "soul_gate"));
        Assert.Contains("empty-handed", client.Texts);
    }

    [Fact]
    public void WalkingOverTheThresholdBeginsIt()
    {
        WorldFixture fx = Field(p =>
        {
            p.Trials[0].BeginOnEnter = true;
            p.Trials[0].Start = null;
            p.Trials[0].ExitTo = new TilePlace { X = 13, Y = 8 };
        });
        FakeConnection client = DeadAndAwake(fx);
        fx.World.Get<DeathSystem>().Move(Alice(fx), 0, new TileCoord(14, 8));
        fx.Ticks(1);
        client.FromClient(new WalkToMessage { X = 18, Y = 8 });
        fx.Ticks(6);
        Assert.True(Trials(fx).Running(Alice(fx), "long_way"));
        Assert.Equal(new TileCoord(18, 8), Alice(fx).Position);
    }

    [Fact]
    public void TheWayTurnsBackThoseItWontHave()
    {
        WorldFixture fx = Field(p =>
        {
            p.Trials[0].BeginOnEnter = true;
            p.Trials[0].ExitTo = new TilePlace { X = 13, Y = 8 };
        });
        FakeConnection client = fx.Login("Alice"); // holds nothing
        fx.World.Get<DeathSystem>().Move(Alice(fx), 0, new TileCoord(14, 8));
        fx.Ticks(1);
        client.FromClient(new WalkToMessage { X = 18, Y = 8 });
        fx.Ticks(6);
        Assert.False(Trials(fx).Running(Alice(fx)));
        Assert.Equal(new TileCoord(13, 8), Alice(fx).Position);
        Assert.Contains("nothing to walk for", client.Texts);
    }

    // ------------------------------------------------------------------ the cold

    [Fact]
    public void TheColdTakesAShareOfMaxBloodAndBloodDoesNotReturn()
    {
        WorldFixture fx = Field();
        DeadAndAwake(fx);
        Assert.True(Trials(fx).Begin(Alice(fx), "soul_gate"));
        Player alice = Alice(fx);
        int max = alice.MaxBlood;
        Assert.Equal(max, alice.Blood);
        fx.Ticks(40);
        // 1% every 2 ticks for 40 ticks: 20 bites of 1% each, and nothing comes back between them.
        int bite = (int)Math.Round(max / 100.0);
        Assert.Equal(max - 20 * bite, alice.Blood);
    }

    [Fact]
    public void LeavingTheWayLosesTheRun()
    {
        WorldFixture fx = Field();
        FakeConnection client = DeadAndAwake(fx);
        var ended = new List<TrialEnded>();
        fx.World.Events.Subscribe<TrialEnded>(ended.Add);
        Assert.True(Trials(fx).Begin(Alice(fx), "soul_gate"));
        fx.World.Get<DeathSystem>().Move(Alice(fx), 0, Wake);
        fx.Ticks(1);
        Assert.False(Trials(fx).Running(Alice(fx)));
        Assert.Single(ended);
        Assert.False(ended[0].Won);
        Assert.Contains("lost", client.Texts);
    }

    [Fact]
    public void DyingOnTheWayKeepsTheHoldAndYouCanTryAgain()
    {
        WorldFixture fx = Field();
        DeadAndAwake(fx);
        Assert.True(Trials(fx).Begin(Alice(fx), "soul_gate"));
        fx.World.Get<CombatSystem>().Damage(Alice(fx), 1_000_000, null, HitKind.Normal);
        Assert.False(Trials(fx).Running(Alice(fx)));
        fx.Ticks(fx.World.Rules.RespawnDelayTicks + 2);
        Assert.Equal(Wake, Alice(fx).Position);
        Assert.True(fx.World.Get<FerrySystem>().Of(Alice(fx)).Holding);
        fx.World.Get<DialogueSystem>().End(Alice(fx));
        Assert.True(Trials(fx).Begin(Alice(fx), "soul_gate"));
    }

    [Fact]
    public void SomeoneOnTheWayWithNoRunIsSentBack()
    {
        WorldFixture fx = Field();
        fx.Login("Alice");
        fx.World.Get<DeathSystem>().Move(Alice(fx), 0, Start);
        fx.Ticks(1);
        Assert.Equal(Wake, Alice(fx).Position);
    }

    // ------------------------------------------------------------------ lanterns

    [Fact]
    public void ALanternWarmsYouOnceAnAttempt()
    {
        WorldFixture fx = Field();
        FakeConnection client = DeadAndAwake(fx);
        Assert.True(Trials(fx).Begin(Alice(fx), "soul_gate"));
        Player alice = Alice(fx);
        alice.Blood = alice.MaxBlood / 2;

        Use(client, fx, "soul_lantern", Lantern, "kindle");
        Assert.True(alice.Blood > alice.MaxBlood * 7 / 10, "the lantern restored " + alice.Blood);

        alice.Blood = alice.MaxBlood / 2;
        Use(client, fx, "soul_lantern", Lantern, "kindle");
        Assert.True(alice.Blood <= alice.MaxBlood / 2, "a spent lantern gave blood again");
        Assert.Contains("spent", client.Texts);

        // A fresh attempt lights it again.
        Trials(fx).End(alice, won: false);
        Assert.True(Trials(fx).Begin(alice, "soul_gate"));
        alice.Blood = alice.MaxBlood / 2;
        Use(client, fx, "soul_lantern", Lantern, "kindle");
        Assert.True(alice.Blood > alice.MaxBlood * 7 / 10);
    }

    // ------------------------------------------------------------------ the walker

    [Fact]
    public void TheWalkerGivesTheHoldBackForNothing()
    {
        WorldFixture fx = Field();
        FakeConnection client = DeadAndAwake(fx);
        Assert.True(Trials(fx).Begin(Alice(fx), "soul_gate"));
        var ended = new List<TrialEnded>();
        fx.World.Events.Subscribe<TrialEnded>(ended.Add);

        fx.World.Get<DialogueSystem>().Start(Alice(fx), "walker", "The walker");
        fx.Ticks(1);
        DialogueMessage line = client.Sent.OfType<DialogueMessage>().Last(m => m.Open);
        client.FromClient(new DialogueChoiceMessage { Option = line.Options.IndexOf("Thank you") });
        fx.Ticks(2);

        Assert.Single(ended);
        Assert.True(ended[0].Won);
        Assert.Equal("sword", fx.World.Get<ItemSystem>().Of(Alice(fx)).Equipment[(int)EquipSlot.Weapon].ItemId);
        Assert.False(fx.World.Get<FerrySystem>().Of(Alice(fx)).Holding);
        Assert.False(fx.World.Get<FerrySystem>().Of(Alice(fx)).InRealm);
        Assert.Equal(Spawn, Alice(fx).Position);
        Assert.Contains(client.Texts, t => t.StartsWith("made it in "));
    }

    // ------------------------------------------------------------------ triggers

    [Fact]
    public void HiddenHandsHoldAndHurtThenRearm()
    {
        WorldFixture fx = Field();
        FakeConnection client = DeadAndAwake(fx);
        Assert.True(Trials(fx).Begin(Alice(fx), "soul_gate"));
        Player alice = Alice(fx);
        Assert.DoesNotContain(client.Sent.OfType<JsonStateMessage>(), m => m.Key == TriggerSystem.StateKey);

        int before = alice.Blood;
        client.FromClient(new WalkToMessage { X = Hands.X + 6, Y = Hands.Y });
        for (int i = 0; i < 30 && alice.Position != Hands; i++) fx.Ticks(1);
        Assert.Equal(Hands, alice.Position);
        Assert.Contains("grabbed", client.Texts);
        Assert.True(alice.RootedUntilTick > fx.World.CurrentTick);
        Assert.Contains(client.Sent.OfType<JsonStateMessage>(), m => m.Key == TriggerSystem.StateKey && m.Json.Contains("hands"));

        // Held where it caught her, then she walks on.
        fx.Ticks(2);
        Assert.Equal(Hands, alice.Position);
        fx.Ticks(10);
        Assert.Equal(new TileCoord(Hands.X + 6, Hands.Y), alice.Position);
        Assert.True(alice.Blood < before - alice.MaxBlood / 10 + alice.MaxBlood / 100 * 10, "the hands hurt: " + (before - alice.Blood));

        // Straight back over it: re-arming takes a while.
        int grabs = client.Texts.Count(t => t == "grabbed");
        client.FromClient(new WalkToMessage { X = Hands.X - 2, Y = Hands.Y });
        fx.Ticks(10);
        Assert.Equal(grabs, client.Texts.Count(t => t == "grabbed"));
    }

    // ------------------------------------------------------------------ shades

    [Fact]
    public void APatrolWalksItsTilesInALoop()
    {
        var route = new List<int[]> { new[] { 20, 2 }, new[] { 26, 2 }, new[] { 26, 6 } };
        WorldFixture fx = Field(spawns: new[] { new NpcSpawn { Npc = "shade", X = 20, Y = 2, Patrol = route } });
        Npc shade = fx.Npc("shade");
        var seen = new HashSet<TileCoord>();
        for (int i = 0; i < 40; i++)
        {
            fx.Ticks(1);
            seen.Add(shade.Position);
        }
        Assert.Contains(new TileCoord(26, 2), seen);
        Assert.Contains(new TileCoord(26, 6), seen);
        Assert.Contains(new TileCoord(20, 2), seen);
    }

    [Fact]
    public void AShadeHitsForAShareOfMaxBloodThenGoesBackToItsRoute()
    {
        var route = new List<int[]> { new[] { 20, 2 }, new[] { 26, 2 } };
        WorldFixture fx = Field(spawns: new[] { new NpcSpawn { Npc = "shade", X = 20, Y = 2, Patrol = route, PatrolMode = "bounce" } });
        DeadAndAwake(fx);
        Assert.True(Trials(fx).Begin(Alice(fx), "soul_gate"));
        Player alice = Alice(fx);
        fx.World.Get<DeathSystem>().Move(alice, 0, new TileCoord(21, 3));
        var hits = new List<DamageDealt>();
        fx.World.Events.Subscribe<DamageDealt>(e => { if (e.Source is Npc) hits.Add(e); });
        fx.Ticks(8);
        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.Equal((int)Math.Round(alice.MaxBlood * 0.2), h.Amount));

        // She gets away; it returns to its route.
        fx.World.Get<DeathSystem>().Move(alice, 0, new TileCoord(40, 12));
        Npc shade = fx.Npc("shade");
        fx.Ticks(30);
        Assert.False(shade.IsDead);
        Assert.Equal(2, shade.Position.Y);
        Assert.InRange(shade.Position.X, 20, 26);
    }

    [Fact]
    public void TrialContentIsChecked()
    {
        Assert.Throws<ContentException>(() => Field(p => p.Trials[0].Gate = "nothing"));
        Assert.Throws<ContentException>(() => Field(p => p.Trials[0].Status = "nothing"));
        Assert.Throws<ContentException>(() => Field(p => p.Npcs.First(n => n.Id == "shade").HitPercent = 150));
    }
}
