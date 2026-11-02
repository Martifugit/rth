using Newtonsoft.Json;
using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Angling;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Skills;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>
/// Trophy angling on the server (docs/fishing.md, AN2-AN6): the rig, trophy spots that come and
/// go and spook, casting with its checks, the fight replayed from the client's inputs, catches
/// (trophy, XP, log, wear), losses, mending, fish markets and their boxes, and leads.
/// </summary>
public class AnglingTests
{
    private static readonly TileCoord Spawn = new(10, 10);
    private const string Skill = "butchering";

    internal static AnglingSpeciesDef Perch() => new()
    {
        Id = "perch", Name = "Perch", Tier = 1, Level = 1, SpotType = "perch_spot", Active = 1, ActiveMinTicks = 500, ActiveMaxTicks = 500,
        SpookAfter = 2, SpookTicks = 100, MinGrams = 200, MaxGrams = 1200, Xp = 50,
        Fight = AnglingFightTests.Perch(),
        Box = { new OpenEntry { Item = "raw_fish", Min = 1, Max = 1 } },
        Notes = { new AnglingNoteDef { Id = "first", Catches = 1, Text = "Perch turn side to side." }, new AnglingNoteDef { Id = "tip", Text = "Perch like the shade." } },
    };

    internal static AnglingSpeciesDef Pike() => new()
    {
        Id = "pike", Name = "Pike", Tier = 3, Level = 20, SpotType = "pike_spot", Active = 1, ActiveMinTicks = 500, ActiveMaxTicks = 500,
        MinGrams = 1000, MaxGrams = 9000, Xp = 200, Teeth = true, MaxThickness = 2,
        Fight = AnglingFightTests.Pike(),
        Box = { new OpenEntry { Item = "raw_fish", Min = 2, Max = 2 } },
    };

    private static ContentParts Parts()
    {
        ContentParts parts = TestContent.Parts();
        parts.Items.Add(new ItemDef { Id = "raw_fish", Name = "Raw fish", Stackable = true });
        parts.Items.Add(new ItemDef { Id = "twine", Name = "Twine", Stackable = true });
        parts.Items.Add(new ItemDef { Id = "rod", Name = "Ash rod", Value = 40, Tackle = new AnglingTackleDef { Slot = AnglingSlot.Rod, SnapBuffer = 24, WearPerFight = 1 } });
        parts.Items.Add(new ItemDef { Id = "reel", Name = "Reel", Value = 40, Tackle = new AnglingTackleDef { Slot = AnglingSlot.Reel, ReelSpeed = 105, RunDrag = 115, WearPerFight = 1, WearPerHard = 5 } });
        parts.Items.Add(new ItemDef { Id = "line", Name = "Flax line", Value = 20, Tackle = new AnglingTackleDef { Slot = AnglingSlot.Line, Strength = 110, Thickness = 2, WearPerFight = 2, WearPerHard = 10,
            Repair = new AnglingRepairDef { Materials = { new ItemCost { Item = "twine", Count = 1 } }, Requirements = { new SkillRequirement { Skill = "occultism", Level = 5 } } } } });
        parts.Items.Add(new ItemDef { Id = "thick_line", Name = "Thick line", Tackle = new AnglingTackleDef { Slot = AnglingSlot.Line, Strength = 140, Thickness = 4 } });
        parts.Items.Add(new ItemDef { Id = "hook", Name = "Hook", Value = 10, Tackle = new AnglingTackleDef { Slot = AnglingSlot.Hook, HookHold = 40, WearPerFight = 2 } });
        parts.Items.Add(new ItemDef { Id = "fly", Name = "Fly", Tackle = new AnglingTackleDef { Slot = AnglingSlot.Lure, Bites = { "perch" } } });
        parts.Items.Add(new ItemDef { Id = "spinner", Name = "Spinner", Tackle = new AnglingTackleDef { Slot = AnglingSlot.Lure, Bites = { "tier:3" } } });
        parts.Items.Add(new ItemDef { Id = "big_rod", Name = "Big rod", Tackle = new AnglingTackleDef { Slot = AnglingSlot.Rod, SnapBuffer = 30, Requirements = { new SkillRequirement { Skill = Skill, Level = 50 } } } });
        parts.Objects.Add(new ObjectDef { Id = "perch_spot", Name = "Perch ripples", Actions = { new ActionDef { Id = "cast", Label = "Cast" } } });
        parts.Objects.Add(new ObjectDef { Id = "pike_spot", Name = "Pike ripples", Actions = { new ActionDef { Id = "cast", Label = "Cast" } } });
        parts.Objects.Add(new ObjectDef { Id = "bench", Name = "Tackle bench", Actions = { new ActionDef { Id = "mend", Label = "Mend" } } });
        parts.Npcs.Add(new NpcDef { Id = "monger", Name = "Fishmonger", Attackable = false, Attacks = false, WanderRadius = 0,
            Actions = { new ActionDef { Id = "hand-in", Label = "Hand in" }, new ActionDef { Id = "mend", Label = "Mend" } } });
        parts.Angling = new AnglingFile
        {
            Skill = Skill,
            Species = { Perch(), Pike() },
            SizeClasses =
            {
                new AnglingSizeClass { Id = "small", Name = "small", UpTo = 300, BoxRolls = 3 },
                new AnglingSizeClass { Id = "good", Name = "good", UpTo = 700, BoxRolls = 5 },
                new AnglingSizeClass { Id = "great", Name = "great", UpTo = 1001, BoxRolls = 8 },
            },
            RevisedChance = 0,
            BiteMinTicks = 2,
            BiteMaxTicks = 2,
        };
        return parts;
    }

    private static readonly PlacedObject[] Placed =
    {
        new() { Type = "perch_spot", X = 11, Y = 10 }, new() { Type = "perch_spot", X = 14, Y = 10 },
        new() { Type = "pike_spot", X = 9, Y = 10 },
        new() { Type = "bench", X = 10, Y = 8 },
    };

    private static (WorldFixture Fx, FakeConnection Alice) World(ContentParts? parts = null)
    {
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), new ServerConfig { RandomSeed = 7 }, GameContent.Create(parts ?? Parts()),
            new[] { new NpcSpawn { Npc = "monger", X = 12, Y = 12 } }, Placed);
        FakeConnection alice = fx.Login("Alice");
        fx.Ticks(1);
        return (fx, alice);
    }

    private static void Give(WorldFixture fx, string player, params string[] items)
    {
        foreach (string item in items) fx.World.Get<ItemSystem>().Give(fx.Player(player), item, 1);
    }

    private static void RigAll(WorldFixture fx, FakeConnection c, string player, params string[] items)
    {
        Give(fx, player, items);
        foreach (string item in items)
        {
            int slot = Slot(fx, player, item);
            c.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = slot, ActionId = ItemActions.Rig });
            fx.Ticks(1);
        }
    }

    private static int Slot(WorldFixture fx, string player, string item)
    {
        var pack = fx.World.Get<ItemSystem>().Of(fx.Player(player)).Inventory;
        for (int i = 0; i < pack.Size; i++)
            if (!pack.IsEmpty(i) && pack[i].ItemId == item) return i;
        return -1;
    }

    private static void Level(WorldFixture fx, string player, int level) =>
        fx.World.Get<SkillSystem>().AddXp(fx.Player(player), Skill, fx.World.Content.XpCurve.XpForLevel(level));

    private static PlacedObject Active(WorldFixture fx, string type) =>
        fx.World.Get<AnglingSpotSystem>().Spots.First(s => s.Placed.Type == type && s.Active).Placed;

    private static void Cast(FakeConnection c, PlacedObject o) =>
        c.FromClient(new ObjectActionMessage { ObjectType = o.Type, ActionId = "cast", Plane = o.Plane, X = o.X, Y = o.Y });

    private static T? State<T>(FakeConnection c, string key) where T : class
    {
        JsonStateMessage? last = c.Sent.OfType<JsonStateMessage>().LastOrDefault(m => m.Key == key);
        return last == null ? null : JsonConvert.DeserializeObject<T>(last.Json);
    }

    /// <summary>
    /// Plays a hooked fight as the client would: rebuilds the fight from the state, plays it with
    /// the bot (or the given inputs), sending a tick's worth of inputs each tick. Returns the final state.
    /// </summary>
    private static AnglingFightState PlayFight(WorldFixture fx, FakeConnection c, Func<AnglingFight, byte>? policy = null)
    {
        AnglingFightState start = State<AnglingFightState>(c, AnglingFightState.StateKey)!;
        Assert.NotEqual("", start.Species);
        var fight = new AnglingFight(start.Fish(), start.Rig);
        var bot = new AnglingBot(5);
        var inputs = new List<byte>();
        int sent = 0;
        for (int tick = 0; tick < 1000 && fx.World.Get<AnglingSystem>().Of(fx.Player("Alice")).Fight != null; tick++)
        {
            for (int i = 0; i < AnglingFight.StepsPerTick && !fight.Over; i++)
            {
                byte input = policy?.Invoke(fight) ?? bot.Decide(fight);
                inputs.Add(input);
                fight.Step(input);
            }
            while (sent < inputs.Count)
            {
                string batch = AnglingInputs.Encode(sent, inputs, sent, IntentMessage.MaxLength, out int taken);
                c.FromClient(new IntentMessage { Kind = AnglingSystem.IntentKind, Arg = batch });
                sent += taken;
            }
            fx.Ticks(1);
        }
        AnglingFightState end = State<AnglingFightState>(c, AnglingFightState.StateKey)!;
        Assert.Equal(fight.Outcome, end.Outcome);
        Assert.Equal(fight.Steps, end.Steps);
        return end;
    }

    // ------------------------------------------------------------------ content

    [Fact]
    public void Each_species_gets_trophies_and_boxes_for_every_size_class()
    {
        GameContent content = GameContent.Create(Parts());
        foreach (string c in new[] { "small", "good", "great", "revised" })
        {
            Assert.Contains(AnglingContent.TrophyTag, content.Items["trophy_perch_" + c].Tags);
            Assert.False(content.Items["trophy_perch_" + c].Stackable);
            Assert.NotNull(content.Items["fishbox_pike_" + c].Open);
        }
        Assert.Equal(8, content.Items["fishbox_pike_great"].Open!.Rolls);
    }

    [Fact]
    public void Bad_angling_content_is_refused()
    {
        ContentParts parts = Parts();
        parts.Angling.Species[0].SpotType = "nowhere";
        parts.Items.Add(new ItemDef { Id = "bad_lure", Name = "Bad lure", Tackle = new AnglingTackleDef { Slot = AnglingSlot.Lure } });
        var e = Assert.Throws<ContentException>(() => GameContent.Create(parts));
        Assert.Contains("spot type 'nowhere'", e.Message);
        Assert.Contains("a lure needs something to bite", e.Message);
    }

    // ------------------------------------------------------------------ the rig

    [Fact]
    public void Parts_go_into_the_rig_and_back_out_and_requirements_hold()
    {
        var (fx, alice) = World();
        RigAll(fx, alice, "Alice", "rod", "line");
        PlayerTackle rig = fx.World.Get<AnglingTackleSystem>().Of(fx.Player("Alice"));
        Assert.Equal("rod", rig.Slots[AnglingSlot.Rod]);
        Assert.Equal(-1, Slot(fx, "Alice", "rod"));
        Assert.Equal(110, fx.World.Get<AnglingTackleSystem>().Stats(fx.Player("Alice")).Strength);

        RigAll(fx, alice, "Alice", "thick_line");      // swaps: the old line comes back
        Assert.Equal("thick_line", rig.Slots[AnglingSlot.Line]);
        Assert.NotEqual(-1, Slot(fx, "Alice", "line"));

        alice.FromClient(new IntentMessage { Kind = AnglingTackleSystem.IntentKind, Arg = "unrig:Rod" });
        fx.Ticks(1);
        Assert.False(rig.Slots.ContainsKey(AnglingSlot.Rod));
        Assert.NotEqual(-1, Slot(fx, "Alice", "rod"));

        RigAll(fx, alice, "Alice", "big_rod");
        Assert.False(rig.Slots.ContainsKey(AnglingSlot.Rod));
        Assert.Contains(alice.Texts, t => t.Contains("level 50"));
    }

    [Fact]
    public void The_rig_is_saved()
    {
        var (fx, alice) = World();
        RigAll(fx, alice, "Alice", "rod", "fly");
        fx.World.Get<AnglingTackleSystem>().Of(fx.Player("Alice")).Condition["rod"] = 42;
        alice.FromClient(new LogoutMessage());
        fx.Ticks(2);
        fx.Login("Alice");
        fx.Ticks(1);
        PlayerTackle rig = fx.World.Get<AnglingTackleSystem>().Of(fx.Player("Alice"));
        Assert.Equal("fly", rig.Slots[AnglingSlot.Lure]);
        Assert.Equal(42, rig.ConditionOf("rod"));
    }

    // ------------------------------------------------------------------ spots

    [Fact]
    public void Each_species_keeps_its_spots_active_and_they_move_on()
    {
        var (fx, _) = World();
        AnglingSpotSystem spots = fx.World.Get<AnglingSpotSystem>();
        Assert.Single(spots.Spots, s => s.Species == "perch" && s.Active);
        var seen = new HashSet<PlacedObject>();
        for (int i = 0; i < 4000; i++)
        {
            fx.Ticks(1);
            Assert.Single(spots.Spots, s => s.Species == "perch" && s.Active);
            seen.Add(spots.Spots.First(s => s.Species == "perch" && s.Active).Placed);
        }
        Assert.Equal(2, seen.Count);
    }

    [Fact]
    public void Casting_checks_level_lure_and_line()
    {
        var (fx, alice) = World();
        RigAll(fx, alice, "Alice", "rod", "fly", "thick_line");
        Cast(alice, Active(fx, "pike_spot"));
        fx.Ticks(4);
        Assert.Contains(alice.Texts, t => t == "Pike requires level 20 Butchering to hook.");

        Level(fx, "Alice", 20);
        Cast(alice, Active(fx, "pike_spot"));
        fx.Ticks(4);
        Assert.Contains(alice.Texts, t => t == "Nothing here will take your fly.");

        RigAll(fx, alice, "Alice", "spinner");
        Cast(alice, Active(fx, "pike_spot"));
        fx.Ticks(4);
        Assert.Contains(alice.Texts, t => t.Contains("Try a thinner line"));

        RigAll(fx, alice, "Alice", "line");
        Cast(alice, Active(fx, "pike_spot"));
        fx.Ticks(6);
        Assert.NotNull(fx.World.Get<AnglingSystem>().Of(fx.Player("Alice")).Fight);
    }

    // ------------------------------------------------------------------ the fight

    [Fact]
    public void A_fish_is_fought_by_the_clients_inputs_and_landed()
    {
        var (fx, alice) = World();
        RigAll(fx, alice, "Alice", "rod", "reel", "line", "hook", "fly");
        Cast(alice, Active(fx, "perch_spot"));
        fx.Ticks(6);
        AnglingFightState end = PlayFight(fx, alice);

        Assert.Equal(AnglingOutcome.Landed, end.Outcome);
        Assert.InRange(end.Grams, 200, 1200);
        Assert.True(end.PersonalBest);
        Assert.Equal(1, fx.World.Get<ItemSystem>().CountOf(fx.Player("Alice"), AnglingContent.TrophyId("perch", end.SizeClass)));
        Assert.True(fx.World.Get<SkillSystem>().Xp(fx.Player("Alice"), Skill) > 0);
        AnglingSpeciesLog log = fx.World.Get<AnglingLogSystem>().Species(fx.Player("Alice"), "perch");
        Assert.Equal(1, log.Caught);
        Assert.Equal(end.Grams, log.BestGrams);
        Assert.Contains("fly", log.Lures);
        Assert.NotEqual(0, log.Moves & (1 << (int)AnglingMove.Left | 1 << (int)AnglingMove.Right));
        Assert.Single(fx.World.Get<AnglingLogSystem>().Notes(fx.Player("Alice"), Perch()));
        // Parts wear from the fight.
        Assert.True(fx.World.Get<AnglingTackleSystem>().Of(fx.Player("Alice")).ConditionOf("line") < 100);
        Assert.Contains(alice.Texts, t => t.Contains("New personal best!"));
    }

    [Fact]
    public void Doing_nothing_loses_the_fish_and_the_spot_spooks_after_its_catches()
    {
        var (fx, alice) = World();
        RigAll(fx, alice, "Alice", "rod", "reel", "line", "hook", "fly");
        PlacedObject spot = Active(fx, "perch_spot");
        Cast(alice, spot);
        fx.Ticks(6);
        AnglingFightState lost = PlayFight(fx, alice, _ => 0);
        Assert.NotEqual(AnglingOutcome.Landed, lost.Outcome);
        Assert.Equal(1, fx.World.Get<AnglingLogSystem>().Species(fx.Player("Alice"), "perch").Lost);

        // It casts again by itself; two catches spook the spot (spookAfter 2).
        for (int caught = 0; caught < 2; caught++)
        {
            for (int i = 0; i < 10 && fx.World.Get<AnglingSystem>().Of(fx.Player("Alice")).Fight == null; i++) fx.Ticks(1);
            Assert.Equal(AnglingOutcome.Landed, PlayFight(fx, alice).Outcome);
        }
        fx.Ticks(5);
        AnglingSpotSystem spots = fx.World.Get<AnglingSpotSystem>();
        Assert.True(spots.IsSpooked(fx.Player("Alice"), spots.SpotAt(spot)!));
        Assert.Null(fx.World.Get<AnglingSystem>().Of(fx.Player("Alice")).Fight);
        Assert.DoesNotContain(State<AnglingSpotsState>(alice, AnglingSpotsState.StateKey)!.Spots, s => s.X == spot.X && s.Y == spot.Y);

        // Spooking is the player's own.
        FakeConnection bob = fx.Login("Bob");
        fx.Ticks(1);
        Assert.Contains(State<AnglingSpotsState>(bob, AnglingSpotsState.StateKey)!.Spots, s => s.X == spot.X && s.Y == spot.Y);
    }

    [Fact]
    public void Walking_away_or_going_quiet_lets_the_fish_go()
    {
        var (fx, alice) = World();
        RigAll(fx, alice, "Alice", "rod", "reel", "line", "hook", "fly");
        Cast(alice, Active(fx, "perch_spot"));
        fx.Ticks(6);
        Assert.NotNull(fx.World.Get<AnglingSystem>().Of(fx.Player("Alice")).Fight);
        fx.Ticks(fx.World.Content.Angling.QuietTicks + 2);
        Assert.Null(fx.World.Get<AnglingSystem>().Of(fx.Player("Alice")).Fight);
        Assert.Equal(AnglingOutcome.Escaped, State<AnglingFightState>(alice, AnglingFightState.StateKey)!.Outcome);
    }

    [Fact]
    public void Inputs_ahead_of_real_time_are_not_stepped()
    {
        var (fx, alice) = World();
        RigAll(fx, alice, "Alice", "rod", "reel", "line", "hook", "fly");
        Cast(alice, Active(fx, "perch_spot"));
        fx.Ticks(6);
        alice.FromClient(new IntentMessage { Kind = AnglingSystem.IntentKind, Arg = "0:1x200" });
        fx.Ticks(1);
        AnglingSystem.Hooked? hooked = fx.World.Get<AnglingSystem>().Of(fx.Player("Alice")).Fight;
        Assert.True(hooked == null || hooked.Fight.Steps < 100);
    }

    // ------------------------------------------------------------------ mending, markets, leads

    [Fact]
    public void Worn_parts_are_mended_at_a_bench_with_the_skill_or_for_coins()
    {
        var (fx, alice) = World();
        RigAll(fx, alice, "Alice", "rod", "line");
        PlayerTackle rig = fx.World.Get<AnglingTackleSystem>().Of(fx.Player("Alice"));
        rig.Condition["line"] = 50;
        rig.Condition["rod"] = 50;
        Give(fx, "Alice", "twine");
        alice.FromClient(new ObjectActionMessage { ObjectType = "bench", ActionId = "mend", X = 10, Y = 8 });
        fx.Ticks(4);
        Assert.Equal(50, rig.ConditionOf("line"));                   // occultism 5 needed
        Assert.Contains(alice.Texts, t => t.Contains("Only the collector") || t.Contains("level 5"));

        fx.World.Get<SkillSystem>().AddXp(fx.Player("Alice"), "occultism", fx.World.Content.XpCurve.XpForLevel(5));
        alice.FromClient(new ObjectActionMessage { ObjectType = "bench", ActionId = "mend", X = 10, Y = 8 });
        fx.Ticks(4);
        Assert.Equal(100, rig.ConditionOf("line"));
        Assert.Equal(0, fx.World.Get<ItemSystem>().CountOf(fx.Player("Alice"), "twine"));

        int fee = fx.World.Get<AnglingTackleSystem>().MendFee(fx.Player("Alice"), collector: false);
        Assert.Equal(10, fee);                                        // the rod: 40 x 50% worn x 50%
        fx.World.Get<ItemSystem>().Give(fx.Player("Alice"), "coins", 100);
        alice.FromClient(new NpcActionMessage { NpcId = fx.Npc("monger").Id, ActionId = "mend" });
        fx.Ticks(5);
        Assert.Equal(100, rig.ConditionOf("rod"));
        Assert.Equal(90, fx.World.Get<ItemSystem>().CountOf(fx.Player("Alice"), "coins"));
    }

    [Fact]
    public void Markets_take_trophies_for_boxes_which_open_into_fish_and_keep_revised_fish()
    {
        var (fx, alice) = World();
        Give(fx, "Alice", "trophy_perch_great", "trophy_pike_small", "trophy_pike_revised");
        alice.FromClient(new NpcActionMessage { NpcId = fx.Npc("monger").Id, ActionId = "hand-in" });
        fx.Ticks(5);
        ItemSystem items = fx.World.Get<ItemSystem>();
        Player p = fx.Player("Alice");
        Assert.Equal(1, items.CountOf(p, "fishbox_perch_great"));
        Assert.Equal(1, items.CountOf(p, "fishbox_pike_small"));
        Assert.Equal(1, items.CountOf(p, "trophy_pike_revised"));

        alice.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = Slot(fx, "Alice", "fishbox_perch_great"), ActionId = ItemActions.Open });
        fx.Ticks(1);
        Assert.Equal(8, items.CountOf(p, "raw_fish"));
        Assert.Equal(0, items.CountOf(p, "fishbox_perch_great"));
    }

    [Fact]
    public void A_lead_names_an_active_spot_and_puts_it_in_the_log()
    {
        var (fx, _) = World();
        Player p = fx.Player("Alice");
        string text = fx.World.Get<AnglingSystem>().LeadText(p, "perch", fx.Npc("monger"));
        Assert.StartsWith("Perch have been rising", text);
        PlacedObject spot = Active(fx, "perch_spot");
        Assert.Equal(AnglingLogSystem.SpotKey(spot), fx.World.Get<AnglingLogSystem>().Species(p, "perch").Lead);
    }
}
