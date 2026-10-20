using Newtonsoft.Json;
using Rth.Server.Content;
using Rth.Shared.Net;
using Rth.Shared.World;

namespace Rth.Tests;

public class WorldMapTests
{
    private static WorldRegion Info(string name, MapData map) =>
        new() { Name = name, Scene = name, X = map.OriginX, Y = map.OriginY, Width = map.Width, Height = map.Height };

    /// <summary>Two open 16x16 regions side by side: west at (0,0), east at (16,0), with a wall in the east one.</summary>
    private static WorldMap TwoRegions()
    {
        var west = new MapData(0, 0, 16, 16);
        var east = new MapData(16, 0, 16, 16);
        east.AddWall(0, 20, 0, WallSide.East);
        var world = new WorldMap();
        world.AddRegion(Info("west", west), west);
        world.AddRegion(Info("east", east), east);
        return world;
    }

    [Fact]
    public void TilesBetweenRegionsAreBlocked()
    {
        WorldMap world = TwoRegions();
        Assert.False(world.IsBlocked(0, 15, 3));
        Assert.False(world.IsBlocked(0, 16, 3));
        Assert.True(world.IsBlocked(0, 40, 3));
        Assert.True(world.IsBlocked(0, -1, 3));
        Assert.True(world.IsBlocked(1, 3, 3)); // no upstairs built
        Assert.False(world.Contains(0, 3, 16));
    }

    [Fact]
    public void PathsCrossRegionBorders()
    {
        WorldMap world = TwoRegions();
        List<TileCoord> path = Pathfinder.FindPath(world, 0, new TileCoord(2, 2), new TileCoord(28, 2));
        Assert.Equal(new TileCoord(28, 2), path[^1]);
        // The wall on the east side of (20, 0) is copied across: a straight step through it is refused.
        Assert.False(world.CanStep(0, 20, 0, 1, 0));
        Assert.False(world.CanStep(0, 21, 0, -1, 0));
    }

    [Fact]
    public void WorksAtNegativeCoordinates()
    {
        var map = new MapData(-16, -8, 16, 8);
        map.Block(0, -9, -1);
        var world = new WorldMap();
        world.AddRegion(Info("south_west", map), map);
        Assert.True(world.IsBlocked(0, -9, -1));
        Assert.False(world.IsBlocked(0, -16, -8));
        Assert.False(world.IsBlocked(0, -1, -1));
        Assert.True(world.IsBlocked(0, 0, 0));
        Assert.Equal("south_west", world.RegionAt(-1, -1)?.Name);
    }

    [Fact]
    public void RefusesOverlappingOrMisalignedRegions()
    {
        WorldMap world = TwoRegions();
        var overlap = new MapData(8, 8, 16, 16);
        Assert.Throws<ArgumentException>(() => world.AddRegion(Info("overlap", overlap), overlap));
        var misaligned = new MapData(40, 0, 12, 16);
        Assert.Throws<ArgumentException>(() => world.AddRegion(Info("odd", misaligned), misaligned));
    }

    [Fact]
    public void RegionDistanceIsZeroInsideAndCountsTilesOutside()
    {
        var region = new WorldRegion { X = 0, Y = 0, Width = 16, Height = 16 };
        Assert.Equal(0, region.DistanceTo(5, 5));
        Assert.Equal(1, region.DistanceTo(16, 5));
        Assert.Equal(10, region.DistanceTo(-3, 25));
    }

    [Fact]
    public void LoaderStitchesEveryRegionInTheFolder()
    {
        string dir = Path.Combine(Path.GetTempPath(), "rth-maps-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            new MapData(0, 0, 16, 16) { Spawn = new TileCoord(3, 4) }.Save(Path.Combine(dir, "home.rthmap"));
            File.WriteAllText(Path.Combine(dir, "home.region.json"), JsonConvert.SerializeObject(new { scene = "Home", title = "Home Town", hasSpawn = true }));
            File.WriteAllText(Path.Combine(dir, "home.spawns.json"), "[ { \"npc\": \"rat\", \"x\": 2, \"y\": 2 } ]");
            new MapData(16, 0, 16, 16).Save(Path.Combine(dir, "road.rthmap"));
            File.WriteAllText(Path.Combine(dir, "road.region.json"), JsonConvert.SerializeObject(new { scene = "Road" }));
            File.WriteAllText(Path.Combine(dir, "road.spawns.json"), "[ { \"npc\": \"rat\", \"x\": 20, \"y\": 2 } ]");

            WorldLoader.Result result = WorldLoader.Load(dir, spawnRegion: null);

            Assert.Equal(new[] { "home", "road" }, result.Map.Regions.Select(r => r.Name));
            Assert.Equal("Home Town", result.Map.Regions[0].Title);
            Assert.Equal("Road", result.Map.Regions[1].Title); // defaults from the name
            Assert.Equal(new TileCoord(3, 4), result.Map.Spawn); // the only region with a spawn
            Assert.Equal(2, result.Placements.Npcs.Count);
            Assert.False(result.Map.IsBlocked(0, 30, 10));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void PlayersLearnTheRegionsAndSeeAcrossTheBorder()
    {
        WorldMap world = TwoRegions();
        world.Spawn = new TileCoord(14, 5);
        var fx = new WorldFixture(world);
        FakeConnection alice = fx.Login("Alice");
        RegionsMessage regions = alice.Sent.OfType<RegionsMessage>().Single();
        Assert.Equal(new[] { "west", "east" }, regions.Regions.Select(r => r.Name));

        FakeConnection bob = fx.Login("Bob");
        bob.FromClient(new WalkToMessage { X = 24, Y = 5 });
        fx.Ticks(12);
        Assert.Equal(new TileCoord(24, 5), fx.Player("Bob").Position);
        // Ten tiles apart across the border: still in view, so never removed.
        Assert.DoesNotContain(alice.Updates, u => u.Removed.Contains(fx.Player("Bob").Id));

        bob.FromClient(new LogoutMessage());
        fx.Ticks(1);
        Assert.Equal("east", fx.Store.Saves["bob"].Map);
    }

    [Fact]
    public void RegionsMessageRoundTrips()
    {
        var message = new RegionsMessage { Regions = { new WorldRegion { Name = "tolling", Scene = "Tolling", Title = "Tolling", X = 0, Y = 0, Width = 96, Height = 96, HasSpawn = true } } };
        byte[] frame = MessageCodec.EncodeFrame(message);
        var decoded = (RegionsMessage)MessageCodec.Decode(frame[4..], frame.Length - 4);
        WorldRegion r = decoded.Regions.Single();
        Assert.Equal(("tolling", "Tolling", 96, true), (r.Name, r.Scene, r.Width, r.HasSpawn));
    }
}
