using Rth.Shared.World;

namespace Rth.Tests;

public class WorldLayoutTests
{
    // A 4 × 3 zone world: sea to the west, then a low plain rising to a high zone in the east.
    private static WorldLayout Small() => WorldLayout.Parse("""
        {
          "zoneTiles": 8, "originX": -16, "originY": 0, "seaLevel": 0,
          "biomes": {
            "~": { "name": "Sea", "ground": "Sea", "water": true, "walkable": false, "rough": 0 },
            "v": { "name": "Plain", "ground": "Grass", "rough": 1 },
            "m": { "name": "Mountain", "ground": "CliffRock", "walkable": false, "rough": 0 }
          },
          "zones":   [ "~vvm", "~vvm", "~vvm" ],
          "heights": [ "024k", "024k", "024k" ]
        }
        """);

    [Fact]
    public void TheShippedLayoutParses()
    {
        string path = Path.Combine(Path.GetDirectoryName(Repo.ServerDirectory)!, "client", "Assets", "World", "layout.json");
        WorldLayout layout = WorldLayout.Parse(File.ReadAllText(path));

        Assert.True(layout.Columns > 100 && layout.Rows > 100);
        Assert.Equal(0, layout.OriginX % layout.ZoneTiles);
        Assert.Equal(0, layout.OriginY % layout.ZoneTiles);
        Assert.False(layout.BiomeAt(48, 48).Water); // Tolling is on land
    }

    [Fact]
    public void MacroHeightPassesThroughZoneCentres()
    {
        WorldLayout layout = Small();
        Assert.Equal(1f, layout.MacroHeight(-16 + 8 + 4, 12), 3);   // second zone: "2" = 1 tile
        Assert.Equal(2f, layout.MacroHeight(-16 + 16 + 4, 12), 3);  // "4" = 2 tiles
        Assert.Equal(1.5f, layout.MacroHeight(-16 + 16, 12), 3);    // halfway between them
        Assert.Equal(10f, layout.MacroHeight(-16 + 24 + 4, 12), 3); // "k" = 20 half tiles
    }

    [Fact]
    public void WaterSitsAtSeaLevelAndShoresMeetIt()
    {
        WorldLayout layout = Small();
        Heightfield field = layout.Bake(-16, 0, 32, 24, seed: 1);
        Assert.Equal(0f, field.Corner(-12, 8));  // in the sea
        Assert.Equal(0f, field.Corner(-8, 8));   // the shore corner touches the sea
        Assert.True(field.Corner(-6, 8) > 0f);   // inland
        Assert.True(layout.BiomeAt(-100, -100).Water, "off the grid is the open sea");
    }

    [Fact]
    public void NeighbouringRegionsMeetExactly()
    {
        WorldLayout layout = Small();
        Heightfield west = layout.Bake(-16, 0, 16, 24, seed: 7);
        Heightfield east = layout.Bake(0, 0, 16, 24, seed: 7);
        for (int y = 0; y <= 24; y++)
            Assert.Equal(west.Corner(0, y), east.Corner(0, y));
    }

    [Fact]
    public void StampsKeepTheirDetailWhenTheLayoutChanges()
    {
        var town = new TerrainStamp { Mode = StampMode.Set, X = 2, Y = 4, Width = 6, Depth = 6, Height = 1.25f, Falloff = 2f };
        WorldLayout before = Small();
        WorldLayout after = Small();
        after.Heights = new List<string> { "08kk", "08kk", "08kk" }; // the land under the town is raised

        Heightfield a = before.Bake(-16, 0, 32, 24, 3, new[] { town });
        Heightfield b = after.Bake(-16, 0, 32, 24, 3, new[] { town });
        for (int y = 4; y <= 10; y++)
            for (int x = 2; x <= 8; x++)
            {
                Assert.Equal(1.25f, a.Corner(x, y), 4);
                Assert.Equal(1.25f, b.Corner(x, y), 4);
            }
        Assert.NotEqual(a.Corner(-4, 20), b.Corner(-4, 20)); // away from the stamp the new layout shows
    }

    [Fact]
    public void LevelFlattensAndFallsOffSmoothly()
    {
        var field = new Heightfield(0, 0, 20, 20);
        field.Fill((x, y) => x * 0.2f);
        new TerrainStamp { Mode = StampMode.Level, X = 6, Y = 6, Width = 4, Depth = 4, Falloff = 3f }.Apply(field);

        float level = field.Corner(6, 6);
        Assert.Equal(1.6f, level, 3); // the average of x 6..10 at 0.2 a tile
        for (int y = 6; y <= 10; y++)
            for (int x = 6; x <= 10; x++)
                Assert.Equal(level, field.Corner(x, y), 4);
        Assert.Equal(0.2f * 14, field.Corner(14, 8), 4);    // beyond the falloff: untouched
        float between = field.Corner(12, 8);                  // inside it: between the two
        Assert.InRange(between, level, 0.2f * 12);
        for (int y = 0; y < 20; y++)
            for (int x = 0; x < 20; x++)
                Assert.False(field.IsSteep(x, y, 1f), $"the falloff made a cliff at {x},{y}");
    }

    [Fact]
    public void AddRaisesARoundHill()
    {
        var field = new Heightfield(0, 0, 30, 30);
        new TerrainStamp { Mode = StampMode.Add, X = 10, Y = 10, Width = 10, Depth = 10, Height = 2f, Round = true, Falloff = 4f }.Apply(field);
        Assert.Equal(2f, field.Corner(15, 15), 4);
        Assert.InRange(field.Corner(10, 10), 0.01f, 1.99f); // the rectangle's corner: on the falloff
        Assert.Equal(0f, field.Corner(6, 15), 4);           // four tiles out from the ellipse: untouched
        Assert.Equal(0f, field.Corner(0, 0), 4);
    }

    [Fact]
    public void BadLayoutsAreRejected()
    {
        Assert.Throws<FormatException>(() => WorldLayout.Parse("""{ "biomes": { "v": {} }, "zones": ["vv"], "heights": ["0"] }"""));
        Assert.Throws<FormatException>(() => WorldLayout.Parse("""{ "biomes": { "v": {} }, "zones": ["vq"], "heights": ["00"] }"""));
        Assert.Throws<FormatException>(() => WorldLayout.Parse("""{ "biomes": { "v": {} }, "zones": ["vv"], "heights": ["0!"] }"""));
    }
}
