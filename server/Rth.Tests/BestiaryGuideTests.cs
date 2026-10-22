using Rth.Server.Content;
using Rth.Server.Game;
using Rth.Shared.Items;
using Rth.Shared.Net;
using Rth.Shared.Progress;
using Rth.Shared.World;

namespace Rth.Tests;

/// <summary>The bestiary's guide: where creatures live, their carcasses, and which drops it hints at.</summary>
public class BestiaryGuideTests
{
    /// <summary>
    /// Test content with drop tables made for the rule:
    /// rat: always a carcass; coins (shared, common) or a fang (3%, also from the boss).
    ///   Its carcass (the "rat" recipe) yields a pelt 1 in 10 (only rats give it).
    /// mite: two rolls of coins or a trinket table; the charm is rare (~2%), the relic only mites give (~17%).
    /// king (boss): always coins; a fang (1%) or the crown (99%, only he gives it).
    /// </summary>
    private static GameContent Content()
    {
        ContentParts parts = TestContent.Parts();
        foreach (string id in new[] { "pelt", "fang", "charm", "relic" }) parts.Items.Add(new ItemDef { Id = id, Name = id });
        parts.DropTables.RemoveAll(t => t.Id == "rat");
        parts.DropTables.Add(new DropTableDef
        {
            Id = "rat",
            Always = { new DropEntry { Item = "carcass" } },
            Entries = { new DropEntry { Item = "coins", Weight = 50 }, new DropEntry { Item = "fang", Weight = 3 } },
            NothingWeight = 47,
        });
        parts.DropTables.Add(new DropTableDef { Id = "rat_yield", Entries = { new DropEntry { Item = "pelt", Weight = 1 } }, NothingWeight = 9 });
        parts.DropTables.Add(new DropTableDef
        {
            Id = "mite", Rolls = 2,
            Entries = { new DropEntry { Item = "coins", Weight = 9 }, new DropEntry { Table = "trinkets", Weight = 1 } },
        });
        parts.DropTables.Add(new DropTableDef { Id = "trinkets", Entries = { new DropEntry { Item = "charm", Weight = 1 }, new DropEntry { Item = "relic", Weight = 9 } } });
        parts.DropTables.Add(new DropTableDef
        {
            Id = "crypt",
            Always = { new DropEntry { Item = "coins" } },
            Entries = { new DropEntry { Item = "fang", Weight = 1 }, new DropEntry { Item = "crown", Weight = 99 } },
        });
        parts.Recipes.Find(r => r.Id == "rat")!.OutputTable = "rat_yield";
        parts.Npcs.Find(n => n.Id == "mite")!.DropTable = "mite";
        parts.Bosses.Add(new BossDef { Id = "king", Name = "King", Region = "arena", Rewards = "crypt" });
        return GameContent.Create(parts);
    }

    private static readonly WorldRegion[] Regions =
    {
        new() { Name = "west", Title = "Westfield", X = 0, Y = 0, Width = 16, Height = 16 },
        new() { Name = "east", Title = "Eastmoor", X = 16, Y = 0, Width = 16, Height = 16 },
        new() { Name = "empty", Title = "Nowhere much", X = 32, Y = 0, Width = 16, Height = 16 },
    };

    private static BestiaryGuide Guide() => BestiaryGuideBuilder.Build(Content(), new[]
    {
        new NpcSpawn { Npc = "rat", X = 2, Y = 2 },
        new NpcSpawn { Npc = "rat", X = 3, Y = 2 },
        new NpcSpawn { Npc = "rat", X = 20, Y = 2 },
        new NpcSpawn { Npc = "mite", X = 3, Y = 3 },
        new NpcSpawn { Npc = "banker", X = 40, Y = 2 },
        new NpcSpawn { Npc = "dummy", X = 500, Y = 500 },
    }, Regions);

    private static BestiaryCreature Creature(BestiaryGuide guide, string id) => guide.Creatures.Single(c => c.Id == id);

    [Fact]
    public void CreaturesLiveWhereTheySpawnAndRegionsWithoutCreaturesAreLeftOut()
    {
        BestiaryGuide guide = Guide();
        Assert.Equal(new[] { "east", "west" }, Creature(guide, "rat").Regions.OrderBy(r => r));
        Assert.Equal(new[] { "west" }, Creature(guide, "mite").Regions);
        Assert.Empty(Creature(guide, "dummy").Regions); // spawned off the map
        Assert.Empty(Creature(guide, "brute").Regions); // never spawned
        Assert.DoesNotContain(guide.Creatures, c => c.Id == "banker"); // townsfolk aren't creatures

        // Tabs by title, the empty region left out, "Elsewhere" last for creatures that live nowhere.
        Assert.Equal(new[] { "Eastmoor", "Westfield", "Elsewhere" }, guide.Regions.Select(r => r.Title));
        Assert.Equal("", guide.Regions[^1].Name);
    }

    [Fact]
    public void ACarcassIsADropARecipeWorks()
    {
        BestiaryGuide guide = Guide();
        BestiaryCarcass carcass = Creature(guide, "rat").Carcass!;
        Assert.Equal("carcass", carcass.Item);
        Assert.Equal("rat", carcass.Recipe);
        Assert.Equal("butchering", carcass.Skill);
        Assert.Equal("rack", carcass.Station);
        Assert.Null(Creature(guide, "mite").Carcass);
    }

    [Fact]
    public void NotableDropsAreTheRareOnesAndTheOnesOnlyThatCreatureGives()
    {
        BestiaryGuide guide = Guide();

        List<NotableDrop> rat = Creature(guide, "rat").Notable;
        // The pelt comes from its carcass: only rats give it, so it's hinted at though it isn't rare.
        NotableDrop pelt = rat.Single(d => d.Item == "pelt");
        Assert.True(pelt.Unique);
        Assert.False(pelt.Rare);
        Assert.Equal(0.1, pelt.Chance, 6);
        // The fang is rare; the boss gives it too, so it isn't unique.
        NotableDrop fang = rat.Single(d => d.Item == "fang");
        Assert.True(fang.Rare);
        Assert.False(fang.Unique);
        Assert.Equal(0.03, fang.Chance, 6);
        // Guaranteed drops (the carcass, the yield's coins) and common shared ones aren't hinted at.
        Assert.Equal(new[] { "pelt", "fang" }, rat.Select(d => d.Item)); // commonest first

        List<NotableDrop> mite = Creature(guide, "mite").Notable;
        // Through the nested table, over two rolls: 1 - (1 - 0.1 * 0.1)^2 for the charm.
        Assert.Equal(1 - Math.Pow(0.99, 2), mite.Single(d => d.Item == "charm").Chance, 6);
        Assert.True(mite.Single(d => d.Item == "charm").Rare);
        NotableDrop relic = mite.Single(d => d.Item == "relic");
        Assert.True(relic.Unique);
        Assert.False(relic.Rare);
        Assert.DoesNotContain(mite, d => d.Item == "coins");

        BestiaryCreature king = guide.Bosses.Single();
        Assert.Equal(new[] { "arena" }, king.Regions);
        Assert.Equal(new[] { "crown", "fang" }, king.Notable.Select(d => d.Item));
        Assert.True(king.Notable[0].Unique);
        Assert.True(king.Notable[1].Rare);
    }

    [Fact]
    public void TheRareChanceIsData()
    {
        ContentParts parts = TestContent.Parts();
        parts.World.RareDropChance = 2;
        Assert.Throws<ContentException>(() => GameContent.Create(parts));
    }

    [Fact]
    public void RarityWords()
    {
        Assert.Equal("Always", DropRarity.Word(1));
        Assert.Equal("Common", DropRarity.Word(0.4));
        Assert.Equal("Uncommon", DropRarity.Word(0.1));
        Assert.Equal("Rare", DropRarity.Word(0.03));
        Assert.Equal("Very rare", DropRarity.Word(0.002));
    }

    [Fact]
    public void TheGuideIsSentOnceAtLoginAndAgainWhenContentChanges()
    {
        var fx = new WorldFixture(Maps.Open(40, 40, new TileCoord(5, 5)), content: Content(), spawns: new[] { new NpcSpawn { Npc = "rat", X = 20, Y = 20 } });
        FakeConnection client = fx.Login("Alice");
        fx.Ticks(3);
        List<JsonStateMessage> sent = client.Sent.OfType<JsonStateMessage>().Where(m => m.Key == BestiarySystem.GuideKey).ToList();
        Assert.Single(sent);
        BestiaryGuide guide = sent[0].Parse<BestiaryGuide>();
        // A bare map has no regions: everything lives "elsewhere".
        Assert.Equal("Elsewhere", guide.Regions.Single().Title);
        Assert.Contains(guide.Creatures.Single(c => c.Id == "rat").Notable, d => d.Item == "pelt");

        fx.World.QueueContent(Content());
        fx.Ticks(2);
        Assert.Equal(2, client.Sent.OfType<JsonStateMessage>().Count(m => m.Key == BestiarySystem.GuideKey));
    }
}
