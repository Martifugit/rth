using Rth.Server;
using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Combat;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Skills;
using Rth.Shared.World;

namespace Rth.Tests;

public class SkillTests
{
    private static readonly TileCoord Spawn = new(5, 5);
    private static readonly PlacedObject Rack = new() { Type = "rack", X = 10, Y = 5 };
    private static readonly PlacedObject Vat = new() { Type = "vat", X = 10, Y = 10 };

    private static WorldFixture World(params NpcSpawn[] spawns)
    {
        MapData map = Maps.Open(40, 40, Spawn);
        map.Block(0, Rack.X, Rack.Y); // workstations stand on blocked tiles
        map.Block(0, Vat.X, Vat.Y);
        return new WorldFixture(map, spawns: spawns, objects: new[] { Rack, Vat });
    }

    private static void Give(WorldFixture fx, string item, int count = 1) =>
        Assert.Equal(0, fx.World.Get<ItemSystem>().Give(fx.Player("Alice"), item, count));

    private static int Count(WorldFixture fx, string item) => fx.World.Get<ItemSystem>().CountOf(fx.Player("Alice"), item);

    private static void Act(FakeConnection client, PlacedObject o, string action = "process") =>
        client.FromClient(new ObjectActionMessage { ObjectType = o.Type, X = o.X, Y = o.Y, ActionId = action });

    // ------------------------------------------------------------------ XP and levels

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 83)]
    [InlineData(10, 1154)]
    [InlineData(50, 101333)]
    [InlineData(92, 6517253)]
    [InlineData(99, 13034431)]
    [InlineData(100, 14391160)]
    public void TheXpCurveIsRuneScapesRunningToOneHundred(int level, long xp)
    {
        var curve = new XpCurve();
        Assert.Equal(xp, curve.XpForLevel(level));
        Assert.Equal(level, curve.LevelForXp(xp));
        if (xp > 0) Assert.Equal(level - 1, curve.LevelForXp(xp - 1));
    }

    [Fact]
    public void LevelsStopAtTheCapAndASkillCanCapLower()
    {
        var curve = new XpCurve();
        Assert.Equal(100, curve.LevelForXp(XpCurve.MaxXp));
        Assert.Equal(curve.XpForLevel(100), curve.XpForLevel(120));
        var shortSkill = new SkillDef { Id = "short", MaxLevel = 20 };
        Assert.Equal(20, curve.LevelForXp(XpCurve.MaxXp, curve.MaxFor(shortSkill)));
        Assert.Equal(100, curve.MaxFor(new SkillDef()));
    }

    [Fact]
    public void LevellingUpAnnouncesItPublishesAnEventAndRaisesStats()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        SkillSystem skills = fx.World.Get<SkillSystem>();
        var levelUps = new List<LevelUp>();
        fx.World.Events.Subscribe<LevelUp>(levelUps.Add);

        skills.AddXp(alice, "might", 220); // past level 3 (210 XP)
        Assert.Equal(3, skills.Level(alice, "might"));
        Assert.Equal(100 + 2 * 3, alice.Stats.Get(StatIds.AbilityDamage)); // might: +3 per level above 1
        Assert.Equal(new[] { 3 }, levelUps.Select(l => l.Level));
        Assert.Contains("Your might is now level 3.", client.Texts);

        fx.Ticks(1);
        Assert.Equal(220, client.Sent.OfType<SkillsMessage>().Last().Xp["might"]);
    }

    [Fact]
    public void OccultDamageTrainsOccultism()
    {
        var fx = World(new NpcSpawn { Npc = "dummy", X = 8, Y = 5 });
        FakeConnection client = fx.Login("Alice");
        client.FromClient(new AttackMessage { TargetId = fx.Npc("dummy").Id });
        client.FromClient(new UseAbilityMessage { AbilityId = "secret" }); // not usable yet: needs occultism 3
        fx.Ticks(1);
        Assert.Contains("You need occultism level 3 to use Secret.", client.Texts);

        // Meanwhile the (occult) auto-attack hit for 50: 0.5 XP per damage in the test rules.
        Player alice = fx.Player("Alice");
        SkillSystem skills = fx.World.Get<SkillSystem>();
        Assert.Equal(25, skills.Xp(alice, "occultism"));

        skills.AddXp(alice, "occultism", 210); // now level 3
        alice.GlobalCooldownReadyTick = 0;
        client.FromClient(new UseAbilityMessage { AbilityId = "secret" });
        fx.Ticks(1);
        Assert.Equal(25 + 210 + 5, skills.Xp(alice, "occultism")); // Secret hit for 10
    }

    [Fact]
    public void EquipmentCanRequireALevel()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "crown");
        PlayerItems items = fx.World.Get<ItemSystem>().Of(fx.Player("Alice"));
        int slot = Enumerable.Range(0, items.Inventory.Size).First(i => items.Inventory[i].ItemId == "crown");

        client.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = slot, ActionId = ItemActions.Equip });
        fx.Ticks(1);
        Assert.Contains("You need occultism level 3 to wear the crown.", client.Texts);
        Assert.True(items.Equipment.IsEmpty((int)EquipSlot.Head));

        fx.World.Get<SkillSystem>().AddXp(fx.Player("Alice"), "occultism", 210);
        client.FromClient(new ItemActionMessage { Container = ContainerKind.Inventory, Slot = slot, ActionId = ItemActions.Equip });
        fx.Ticks(1);
        Assert.Equal("crown", items.Equipment[(int)EquipSlot.Head].ItemId);
    }

    [Fact]
    public void SkillsSurviveLogoutIncludingSkillsThisServerDoesntKnow()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        fx.World.Get<SkillSystem>().AddXp(fx.Player("Alice"), "butchering", 333);
        client.FromClient(new LogoutMessage());
        fx.Ticks(1);
        fx.Store.Saves["alice"].Sections["skills"]!["dreaming"] = 50;

        FakeConnection again = fx.Login("Alice");
        Assert.Equal(333, fx.World.Get<SkillSystem>().Xp(fx.Player("Alice"), "butchering"));
        again.FromClient(new LogoutMessage());
        fx.Ticks(1);
        Assert.Equal(50, (double)fx.Store.Saves["alice"].Sections["skills"]!["dreaming"]!);
    }

    // ------------------------------------------------------------------ objects and recipes

    [Fact]
    public void ButcheringWalksToTheStationAndProcessesEveryCarcass()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "knife");
        Give(fx, "carcass", 3);

        Act(client, Rack);
        fx.Ticks(4); // walk 4 tiles to stand beside the rack
        Assert.True(fx.World.IsBeside(fx.Player("Alice"), 0, Rack.Tile));
        Assert.Contains("You begin to butcher the rat carcass.", client.Texts);

        fx.Ticks(9); // three carcasses, 3 ticks each
        Assert.Equal(0, Count(fx, "carcass"));
        Assert.Equal(6, Count(fx, "coins"));
        Assert.Equal(3, Count(fx, "gem")); // one output-table roll each at level 1
        Assert.Equal(150, fx.World.Get<SkillSystem>().Xp(fx.Player("Alice"), "butchering"));
        Assert.Contains("You have nothing left to butcher.", client.Texts);
        Assert.Null(fx.Player("Alice").Interaction);
    }

    [Fact]
    public void HigherLevelsRollTheOutputTableMoreOften()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        fx.World.Get<SkillSystem>().AddXp(alice, "butchering", new XpCurve().XpForLevel(11));
        alice.Position = new TileCoord(9, 5);
        Give(fx, "knife");
        Give(fx, "carcass");

        Act(client, Rack);
        fx.Ticks(4);
        Assert.Equal(3, Count(fx, "gem")); // level 11: one extra roll per 5 levels above 1, so 1 + 2
    }

    [Fact]
    public void ButcheringNeedsTheToolTheLevelAndSomethingToButcher()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        alice.Position = new TileCoord(9, 5);

        Act(client, Rack);
        fx.Ticks(1);
        Assert.Contains("You have nothing to butcher here.", client.Texts);

        Give(fx, "carcass");
        Act(client, Rack);
        fx.Ticks(1);
        Assert.Contains("You need a knife to butcher the rat carcass.", client.Texts);
        Assert.Equal(1, Count(fx, "carcass"));

        alice.Position = new TileCoord(10, 9);
        Give(fx, "big_carcass");
        Act(client, Vat);
        fx.Ticks(1);
        Assert.Contains("You need butchering level 5 to butcher the big carcass.", client.Texts);
    }

    [Fact]
    public void FullInventoryOutputsGoOnTheGround()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Player alice = fx.Player("Alice");
        alice.Position = new TileCoord(10, 9);
        fx.World.Get<SkillSystem>().AddXp(alice, "butchering", 1000);
        Give(fx, "big_carcass");
        Give(fx, "carcass", 27); // full

        Act(client, Vat);
        fx.Ticks(3);
        Assert.Equal(0, Count(fx, "big_carcass"));
        Assert.Equal(1, Count(fx, "gem")); // into the slot the carcass left...
        GroundItem gem = fx.World.Get<GroundItemSystem>().Items.Single(); // ...and the second one on the floor
        Assert.Equal(("gem", alice.Position), (gem.ItemId, gem.Position));
    }

    [Fact]
    public void ObjectActionsMustMatchARealObject()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        Give(fx, "knife");
        Give(fx, "carcass");

        client.FromClient(new ObjectActionMessage { ObjectType = "rack", X = 11, Y = 5, ActionId = "process" }); // nothing there
        client.FromClient(new ObjectActionMessage { ObjectType = "rack", X = 10, Y = 5, ActionId = "steal" });   // not an action it has
        fx.Ticks(1);
        Assert.Null(fx.Player("Alice").Interaction);

        Act(client, Rack, ObjectSystem.Examine);
        fx.Ticks(1);
        Assert.Contains("A rack.", client.Texts);
    }

    [Fact]
    public void SignpostsAndGatesSpeakWithoutCode()
    {
        var sign = new PlacedObject { Type = "sign", X = 6, Y = 6 };
        var gate = new PlacedObject { Type = "gate", X = 7, Y = 6 };
        var fx = new WorldFixture(Maps.Open(40, 40, Spawn), objects: new[] { sign, gate });
        FakeConnection client = fx.Login("Alice");

        Act(client, sign, "read");
        Act(client, gate, "open");
        fx.Ticks(1);
        Assert.Equal(new[] { "Tolling, west.", "Barred." }, client.Texts.ToArray());

        fx.World.Get<FlagSystem>().Set(fx.Player("Alice"), "gate_open", "1");
        Act(client, gate, "open");
        fx.Ticks(1);
        Assert.Equal("It creaks open.", client.Texts.Last());
    }

    [Fact]
    public void WalkingAwayStopsWork()
    {
        var fx = World();
        FakeConnection client = fx.Login("Alice");
        fx.Player("Alice").Position = new TileCoord(9, 5);
        Give(fx, "knife");
        Give(fx, "carcass", 5);
        Act(client, Rack);
        fx.Ticks(4);
        client.FromClient(new WalkToMessage { X = 2, Y = 2 });
        fx.Ticks(10);
        Assert.Equal(4, Count(fx, "carcass"));
    }

    [Fact]
    public void CarcassesFromKillsCanBeButchered()
    {
        // End to end with the shipped content: a fen rat drops a carcass; the rack turns it into loot.
        GameContent content = GameContent.Load(Repo.ContentDirectory);
        MapData map = Maps.Open(40, 40, Spawn);
        map.Block(0, 12, 5);
        var fx = new WorldFixture(map, content: content,
            spawns: new[] { new NpcSpawn { Npc = "fen_rat", X = 8, Y = 5, Wander = 0 } },
            objects: new[] { new PlacedObject { Type = "skinning_rack", X = 12, Y = 5 } });
        FakeConnection client = fx.Login("Alice");
        Npc rat = fx.Npc("fen_rat");

        client.FromClient(new AttackMessage { TargetId = rat.Id });
        for (int i = 0; i < 100 && !rat.IsDead; i++) fx.Ticks(1);
        GroundItem carcass = fx.World.Get<GroundItemSystem>().Items.Single();
        Assert.Equal("fen_rat_carcass", carcass.ItemId);

        client.FromClient(new PickupMessage { GroundItemId = carcass.Id });
        fx.Ticks(6);
        Assert.Equal(1, Count(fx, "fen_rat_carcass"));
        Assert.True(Count(fx, "skinning_knife") == 1, "New characters start with a skinning knife.");

        client.FromClient(new ObjectActionMessage { ObjectType = "skinning_rack", X = 12, Y = 5, ActionId = "process" });
        fx.Ticks(15);
        Assert.Equal(0, Count(fx, "fen_rat_carcass"));
        Assert.Equal(1, Count(fx, "rat_pelt"));
        Assert.True(fx.World.Get<SkillSystem>().Xp(fx.Player("Alice"), "butchering") > 0);
        Assert.True(fx.World.Get<SkillSystem>().Xp(fx.Player("Alice"), "occultism") > 0);
    }
}
