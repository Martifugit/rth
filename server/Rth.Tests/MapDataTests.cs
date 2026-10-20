using Rth.Shared.World;

namespace Rth.Tests;

public class MapDataTests
{
    [Fact]
    public void RoundTripsThroughTheFileFormat()
    {
        var map = new MapData(-4, 10, 6, 5, planes: 2) { SpawnPlane = 1, Spawn = new TileCoord(-2, 12) };
        map.Block(0, -4, 10);
        map.Block(1, 1, 14);
        map.AddWall(0, -1, 11, WallSide.East);
        map.AddWall(1, -3, 12, WallSide.North);

        using var stream = new MemoryStream();
        map.Write(stream);
        stream.Position = 0;
        MapData loaded = MapData.Read(stream);

        Assert.Equal((map.OriginX, map.OriginY, map.Width, map.Height, map.Planes), (loaded.OriginX, loaded.OriginY, loaded.Width, loaded.Height, loaded.Planes));
        Assert.Equal(1, loaded.SpawnPlane);
        Assert.Equal(new TileCoord(-2, 12), loaded.Spawn);
        for (int plane = 0; plane < 2; plane++)
            for (int y = 10; y < 15; y++)
                for (int x = -4; x < 2; x++)
                    Assert.Equal(map.GetFlags(plane, x, y), loaded.GetFlags(plane, x, y));
    }

    [Fact]
    public void RejectsFilesThatAreNotMaps()
    {
        using var stream = new MemoryStream(new byte[64]);
        Assert.Throws<InvalidDataException>(() => MapData.Read(stream));
    }
}
