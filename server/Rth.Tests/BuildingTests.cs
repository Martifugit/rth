using Rth.Shared.World;
using Rth.Shared.World.Data;
using Xunit;

namespace Rth.Tests;

/// <summary>
/// Building pieces (<see cref="PieceKind.Building"/>): the walls a kit's building puts on the map,
/// open at its doorways, what blocks outside them, its checks, and how it is written to a file.
/// Uses a kit of its own, so changing world/kits never breaks these.
/// </summary>
public class BuildingTests
{
    private static readonly BuildingKit Kit = new()
    {
        Id = "testkit",
        Wall = "Wall",
        MaxSpan = 8,
        Pieces = { "Wall", "WallWindow", "WallDoorway", "Door", "Woodpile", "Lantern" },
        Openings = { ["WallDoorway"] = "Door" },
        Attachments = { ["Woodpile"] = new() { "Wall" }, ["Lantern"] = new() { "WallDoorway" } },
        Blocking = { "Woodpile" },
    };

    public BuildingTests() => BuildingKits.Register(Kit);

    private static RegionData Region(Piece piece)
    {
        var region = new RegionData
        {
            Id = "test", Title = "Test", Scene = "Assets/Scenes/Test.unity",
            Origin = new[] { 0, 0 }, Size = new[] { 16, 16 }, Ground = "Grass",
            Pieces = new List<Piece> { piece },
        };
        region.Materials["Grass"] = "#808080";
        return region;
    }

    private static Piece Cottage(BuildingSpec spec) =>
        new() { Id = "cottage", Kind = PieceKind.Building, Rect = new[] { 4, 4, 8, 8 }, Building = spec };

    private static BuildingSpec Spec(params (string face, string[] tiles)[] faces)
    {
        var spec = new BuildingSpec { Kit = "testkit", Faces = new SortedDictionary<string, List<string>>() };
        foreach ((string face, string[] tiles) in faces) spec.Faces[face] = tiles.ToList();
        return spec;
    }

    private static RegionCompiler.Result Compile(Piece piece)
    {
        RegionCompiler.Result r = RegionCompiler.Compile(Region(piece), null);
        Assert.True(r.Errors.Count == 0, string.Join("\n", r.Errors));
        return r;
    }

    [Fact]
    public void ItsWallsRunAlongItsEdgesOpenOnlyAtTheDoorway()
    {
        MapData map = Compile(Cottage(Spec(("S", new[] { "WallWindow", "WallDoorway+Lantern", "", "WallWindow" })))).Map;
        Assert.True(map.CanStep(0, 5, 3, 0, 1));   // in at the doorway, the second tile of the south face
        Assert.False(map.CanStep(0, 4, 3, 0, 1));  // a window is a wall
        Assert.False(map.CanStep(0, 6, 3, 0, 1));
        Assert.False(map.CanStep(0, 5, 7, 0, 1));  // the north wall
        Assert.False(map.CanStep(0, 7, 5, 1, 0));  // the east wall
        Assert.False(map.CanStep(0, 4, 5, -1, 0)); // the west wall
        Assert.True(map.CanStep(0, 5, 5, 1, 0));   // the floor inside is open
        Assert.False(map.IsBlocked(0, 6, 6));
    }

    [Fact]
    public void AnEastWestBuildingWithADoorInItsEndIsEnteredThere()
    {
        MapData map = Compile(Cottage(Spec(("W", new[] { "", "", "WallDoorway", "" })))).Map;
        Assert.True(map.CanStep(0, 3, 6, 1, 0));
        Assert.False(map.CanStep(0, 3, 5, 1, 0));
    }

    [Fact]
    public void ABuildingOnSteepGroundHasAWalkableFloor()
    {
        RegionData region = Region(Cottage(Spec(("S", new[] { "Wall", "WallDoorway", "Wall", "Wall" }))));
        // Ground climbing a tile and a half a tile from west to east: every tile a cliff.
        var field = new Heightfield(0, 0, 16, 16);
        for (int y = 0; y <= 16; y++) for (int x = 0; x <= 16; x++) field.SetCorner(x, y, x * 1.5f);
        region.Terrain = TerrainText.Write(field);
        RegionCompiler.Result r = RegionCompiler.Compile(region, null);
        Assert.True(r.Errors.Count == 0, string.Join("\n", r.Errors));
        for (int y = 4; y < 8; y++) for (int x = 4; x < 8; x++) Assert.False(r.Map.IsBlocked(0, x, y), $"floor ({x}, {y}) blocked");
        Assert.True(r.Map.IsBlocked(0, 2, 2));   // the slope outside is still a cliff
    }

    [Fact]
    public void AWoodpileAndAChimneyBlockTheTilesOutsideTheirWalls()
    {
        BuildingSpec spec = Spec(("S", new[] { "WallDoorway" }), ("N", new[] { "", "Wall+Woodpile" }));
        spec.Chimney = "W1";
        MapData map = Compile(Cottage(spec)).Map;
        Assert.True(map.IsBlocked(0, 5, 8));   // the woodpile, north of the north face's second tile
        Assert.True(map.IsBlocked(0, 3, 5));   // the chimney breast, west of the west end's second tile
        Assert.False(map.IsBlocked(0, 3, 4));
    }

    [Fact]
    public void ABuildingWithNoDoorwayIsWarnedAbout()
    {
        Assert.Contains(Compile(Cottage(Spec())).Warnings, w => w.Contains("no doorway"));
    }

    [Theory]
    [InlineData("an odd gable span", "span")]
    [InlineData("an unknown piece", "doesn't have")]
    [InlineData("a lantern on a plain wall", "can't hang")]
    [InlineData("a face longer than its side", "lists 5 tiles")]
    [InlineData("a chimney in the long side", "gable end")]
    [InlineData("an unknown kit", "isn't in")]
    public void WhatCantBeBuiltIsAnError(string what, string message)
    {
        Piece piece = Cottage(Spec(("S", new[] { "WallDoorway" })));
        switch (what)
        {
            case "an odd gable span": piece.Rect = new[] { 4, 4, 9, 7 }; break;
            case "an unknown piece": piece.Building!.Faces!["N"] = new List<string> { "WallTurret" }; break;
            case "a lantern on a plain wall": piece.Building!.Faces!["N"] = new List<string> { "Wall+Lantern" }; break;
            case "a face longer than its side": piece.Building!.Faces!["N"] = new List<string> { "", "", "", "", "" }; break;
            case "a chimney in the long side": piece.Rect = new[] { 4, 4, 9, 8 }; piece.Building!.Chimney = "S1"; break;
            case "an unknown kit": piece.Building!.Kit = "nokit"; break;
        }
        Assert.Contains(RegionCompiler.Compile(Region(piece), null).Errors, e => e.Contains(message));
    }

    [Fact]
    public void ItsFacesAreWrittenOneToALineAndReadBack()
    {
        BuildingSpec spec = Spec(("S", new[] { "WallWindow", "WallDoorway" }), ("N", new[] { "", "Wall+Woodpile" }));
        spec.Chimney = "W1";
        string text = RegionFiles.Format(Region(Cottage(spec)));
        Assert.Contains("\n        \"S\": [\"WallWindow\", \"WallDoorway\"]", text);
        RegionData back = RegionFiles.ParseRegion(text);
        Assert.Equal(text, RegionFiles.Format(back));
        Assert.Equal("W1", back.Pieces[0].Building!.Chimney);
    }
}
