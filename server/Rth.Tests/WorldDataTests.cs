using Rth.Shared.World;
using Rth.Shared.World.Data;
using Xunit;

namespace Rth.Tests;

/// <summary>
/// The world data (world/regions, docs/workflow.md): what each kind of piece does to the map, the
/// file layout, the hand-made overlay, and that the shipped data and server maps agree.
/// </summary>
public class WorldDataTests
{
    private static RegionData Region(params Piece[] pieces)
    {
        var region = new RegionData
        {
            Id = "test",
            Title = "Test",
            Scene = "Assets/Scenes/Test.unity",
            Origin = new[] { 0, 0 },
            Size = new[] { 16, 16 },
            Ground = "Grass",
            Pieces = pieces.ToList(),
        };
        foreach (string m in new[] { "Grass", "Stone", "Water", "Wood", "Dirt" }) region.Materials[m] = "#808080";
        return region;
    }

    private static RegionCompiler.Result Compile(params Piece[] pieces)
    {
        RegionCompiler.Result r = RegionCompiler.Compile(Region(pieces), null);
        Assert.True(r.Errors.Count == 0, string.Join("\n", r.Errors));
        return r;
    }

    [Fact]
    public void GroundCoversTheRegionAndTheOutsideIsBlocked()
    {
        MapData map = Compile().Map;
        Assert.False(map.IsBlocked(0, 0, 0));
        Assert.False(map.IsBlocked(0, 15, 15));
        Assert.True(map.IsBlocked(0, 16, 0));
    }

    [Fact]
    public void WithoutGroundEveryTileIsBlockedUntilAPatchGivesSome()
    {
        RegionData region = Region(new Piece { Id = "square", Kind = PieceKind.Patch, Rect = new[] { 2, 2, 4, 4 }, Material = "Stone" });
        region.Ground = null;
        MapData map = RegionCompiler.Compile(region, null).Map;
        Assert.True(map.IsBlocked(0, 1, 1));
        Assert.False(map.IsBlocked(0, 2, 2));
        Assert.False(map.IsBlocked(0, 3, 3));
        Assert.True(map.IsBlocked(0, 4, 4));
    }

    [Fact]
    public void AFenceBlocksEdgesExceptAtItsGaps()
    {
        // Along y = 5 from x 2 to 6 with a gate at 4: the edges between rows 4 and 5 at x 2, 3 and 5.
        MapData map = Compile(new Piece { Id = "fence", Kind = PieceKind.Fence, From = new[] { 2, 5 }, To = new[] { 6, 5 }, Gaps = new List<int> { 4 }, Material = "Wood" }).Map;
        Assert.False(map.CanStep(0, 2, 4, 0, 1));
        Assert.False(map.CanStep(0, 3, 5, 0, -1));
        Assert.True(map.CanStep(0, 4, 4, 0, 1));
        Assert.False(map.CanStep(0, 5, 4, 0, 1));
        Assert.True(map.CanStep(0, 6, 4, 0, 1));
        Assert.True(map.CanStep(0, 1, 4, 0, 1));
    }

    [Fact]
    public void AHouseHasWallsAndOneDoor()
    {
        MapData map = Compile(new Piece
        {
            Id = "house", Kind = PieceKind.House, Rect = new[] { 4, 4, 8, 7 }, Door = "S", DoorAt = 5,
            Walls = "Stone", Floor = "Wood", Roof = "Dirt",
        }).Map;
        Assert.True(map.CanStep(0, 5, 3, 0, 1)); // through the door
        Assert.False(map.CanStep(0, 4, 3, 0, 1));
        Assert.False(map.CanStep(0, 6, 6, 0, 1)); // north wall
        Assert.False(map.CanStep(0, 3, 5, 1, 0)); // west wall
        Assert.False(map.CanStep(0, 7, 5, 1, 0)); // east wall
        Assert.False(map.IsBlocked(0, 6, 5)); // the floor is walkable
    }

    [Fact]
    public void TreesRocksAndObjectsBlockTheirTileButNpcsAndSpawnsDont()
    {
        RegionCompiler.Result r = Compile(
            new Piece { Id = "tree", Kind = PieceKind.Tree, At = new[] { 1, 1 } },
            new Piece { Id = "rock", Kind = PieceKind.Rock, At = new[] { 2, 1 } },
            new Piece { Id = "still", Kind = PieceKind.Prop, At = new[] { 3, 1 }, Object = "still" },
            new Piece { Id = "bench", Kind = PieceKind.Prop, At = new[] { 4, 1 } },
            new Piece { Id = "rat", Kind = PieceKind.Npc, At = new[] { 5, 1 }, Npc = "fen_rat", Wander = 3 },
            new Piece { Id = "spawn", Kind = PieceKind.Spawn, At = new[] { 6, 1 } });
        Assert.True(r.Map.IsBlocked(0, 1, 1));
        Assert.True(r.Map.IsBlocked(0, 2, 1));
        Assert.True(r.Map.IsBlocked(0, 3, 1));
        Assert.False(r.Map.IsBlocked(0, 4, 1)); // a prop that isn't an object blocks nothing unless it has a footprint
        Assert.False(r.Map.IsBlocked(0, 5, 1));
        Assert.Equal(new TileCoord(6, 1), r.Map.Spawn);
        Assert.True(r.HasSpawn);
        Assert.Equal("still", Assert.Single(r.Objects).Type);
        NpcEntry rat = Assert.Single(r.Npcs);
        Assert.Equal((5, 1, 3), (rat.X, rat.Y, rat.Wander));
    }

    [Fact]
    public void AFootprintTurnsWithThePiece()
    {
        // Two tiles east of the anchor; turned a quarter clockwise, they run south of it.
        var piece = new Piece { Id = "cart", Kind = PieceKind.Prop, At = new[] { 8, 8 }, Footprint = new[] { 0, 0, 2, 1 } };
        Assert.Equal(new[] { (8, 8), (9, 8) }, RegionCompiler.FootprintTiles(piece));
        piece.Rot = 90;
        Assert.Equal(new[] { (8, 8), (8, 7) }, RegionCompiler.FootprintTiles(piece));
        piece.Rot = 180;
        Assert.Equal(new[] { (8, 8), (7, 8) }, RegionCompiler.FootprintTiles(piece));
    }

    [Fact]
    public void WaterBlocksAndRoadsGiveGround()
    {
        RegionData region = Region(
            new Piece { Id = "river", Kind = PieceKind.Water, Line = new List<float[]> { new[] { 8f, 0f }, new[] { 8f, 16f } }, Width = 0.6f, Material = "Water" },
            new Piece { Id = "road", Kind = PieceKind.Road, Line = new List<float[]> { new[] { 0f, 12.5f }, new[] { 16f, 12.5f } }, Width = 0.5f, Material = "Dirt" });
        region.Ground = null;
        MapData map = RegionCompiler.Compile(region, null).Map;
        Assert.True(map.IsBlocked(0, 8, 3)); // river (no ground there either)
        Assert.False(map.IsBlocked(0, 2, 12)); // road
        Assert.True(map.IsBlocked(0, 2, 11));
        Assert.True(map.IsBlocked(0, 8, 12)); // the river crosses the road: water wins
    }

    [Fact]
    public void AnUpperFloorIsItsOwnPlane()
    {
        RegionCompiler.Result r = Compile(new Piece { Id = "loft", Kind = PieceKind.Patch, Plane = 1, Rect = new[] { 2, 2, 4, 4 }, Material = "Wood" });
        Assert.Equal(2, r.Map.Planes);
        Assert.False(r.Map.IsBlocked(1, 2, 2));
        Assert.True(r.Map.IsBlocked(1, 5, 5));
    }

    [Fact]
    public void MistakesAreReportedNotThrown()
    {
        RegionData region = Region(
            new Piece { Id = "a", Kind = PieceKind.Tree, At = new[] { 40, 40 } },
            new Piece { Id = "a", Kind = "castle" },
            new Piece { Id = "b", Kind = PieceKind.Patch, Rect = new[] { 2, 2, 1, 4 }, Material = "Marble" },
            new Piece { Id = "c", Kind = PieceKind.Wall, From = new[] { 1, 1 }, To = new[] { 3, 4 }, Material = "Stone" });
        List<string> errors = RegionCompiler.Compile(region, null).Errors;
        Assert.Contains(errors, e => e.Contains("outside the region"));
        Assert.Contains(errors, e => e.Contains("used twice"));
        Assert.Contains(errors, e => e.Contains("unknown kind"));
        Assert.Contains(errors, e => e.Contains("rect is empty"));
        Assert.Contains(errors, e => e.Contains("one grid line"));
    }

    [Fact]
    public void MovingAPieceMovesEveryCoordinate()
    {
        var house = new Piece { Id = "h", Kind = PieceKind.House, Rect = new[] { 4, 4, 8, 7 }, Door = "S", DoorAt = 5 };
        house.Translate(2, 3);
        Assert.Equal(new[] { 6, 7, 10, 10 }, house.Rect);
        Assert.Equal(7, house.DoorAt);
        var fence = new Piece { Id = "f", Kind = PieceKind.Fence, From = new[] { 2, 5 }, To = new[] { 2, 9 }, Gaps = new List<int> { 7 } };
        fence.Translate(1, -1);
        Assert.Equal(new[] { 3, 4 }, fence.From);
        Assert.Equal(new List<int> { 6 }, fence.Gaps);
    }

    [Fact]
    public void TheFileLayoutIsStable()
    {
        RegionData region = Region(
            new Piece { Id = "tree", Kind = PieceKind.Tree, At = new[] { 1, 1 }, Scale = 1.15f, Parts = new List<Part> { new Part { At = new[] { 0f, 0.8f, 0f }, Size = new[] { 0.32f, 1.6f, 0.32f }, Material = "Wood" } } },
            new Piece { Id = "rat", Kind = PieceKind.Npc, At = new[] { 5, 1 }, Npc = "fen_rat" });
        string text = RegionFiles.Format(region);
        Assert.Equal(text, RegionFiles.Format(RegionFiles.ParseRegion(text)));
        Assert.Contains("\"scale\": 1.15,", text); // floats rounded, not 1.14999998
        Assert.Contains("    { \"id\": \"rat\", \"kind\": \"npc\", \"at\": [5, 1], \"npc\": \"fen_rat\" }", text);
    }

    [Fact]
    public void HandmadeLayersSurviveTheTextForm()
    {
        var layers = new CollisionLayers(8, 16, 8, 8, 2);
        layers.SetGround(0, 9, 17);
        layers.SetGround(0, 10, 17);
        layers.Block(0, 10, 17);
        layers.AddWall(0, 9, 17, WallSide.North);
        layers.AddWall(0, 8, 16, WallSide.South); // on the border: kept from this side
        layers.AddWall(0, 8, 20, WallSide.West);
        layers.SetGround(1, 12, 20);
        var copy = new CollisionLayers(8, 16, 8, 8);
        copy.AddText(layers.ToText(0));
        copy.AddText(layers.ToText(1));
        for (int p = 0; p < 2; p++)
            for (int y = 16; y < 24; y++)
                for (int x = 8; x < 16; x++)
                    Assert.Equal(layers.GetFlags(p, x, y), copy.GetFlags(p, x, y));
    }

    [Fact]
    public void HandmadeContentAddsToTheData()
    {
        RegionData region = Region(new Piece { Id = "rock", Kind = PieceKind.Rock, At = new[] { 2, 2 } });
        var extra = new CollisionLayers(0, 0, 16, 16);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++) extra.SetGround(0, x, y);
        extra.Block(0, 5, 5);
        var handmade = new HandmadeData
        {
            Planes = { extra.ToText(0) },
            Objects = { new ObjectEntry { Type = "well", X = 5, Y = 5 } },
            Spawn = new[] { 0, 7, 7 },
        };
        RegionCompiler.Result r = RegionCompiler.Compile(region, handmade);
        Assert.Empty(r.Errors);
        Assert.True(r.Map.IsBlocked(0, 2, 2));
        Assert.True(r.Map.IsBlocked(0, 5, 5));
        Assert.Equal("well", Assert.Single(r.Objects).Type);
        Assert.Equal(new TileCoord(7, 7), r.Map.Spawn);
    }

    /// <summary>
    /// The shipped world data compiles cleanly, names only content that exists, and
    /// server/data/maps is exactly what it compiles to: run `dotnet run --project tools/world -- export`.
    /// </summary>
    [Fact]
    public void TheShippedWorldDataIsValidAndExported()
    {
        WorldWorkspace ws = WorldWorkspace.Load(Path.GetDirectoryName(Repo.ServerDirectory)!);
        List<string> errors = ws.Check();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }
}
