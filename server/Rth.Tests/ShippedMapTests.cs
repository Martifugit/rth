using Rth.Server.Content;
using Rth.Shared.World;
using Xunit;

namespace Rth.Tests;

/// <summary>Checks on every region the exporter has written to server/data/maps.</summary>
public class ShippedMapTests
{
    private static readonly string Maps = Path.Combine(Repo.ServerDirectory, "data", "maps");

    private static readonly (int dx, int dy)[] Steps = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    /// <summary>
    /// Walks each region from its spawn (within it), through every stair, ladder, trapdoor and shortcut it
    /// reaches (whatever they need), and asserts every object in it can be stood beside: a
    /// station in the middle of a table, or a sign behind a wall, can never be used.
    /// </summary>
    [Fact]
    public void EveryObjectCanBeReachedFromItsRegionsSpawn()
    {
        WorldMap world = WorldLoader.Load(Maps, null).Map;
        var problems = new List<string>();
        foreach (string path in Directory.GetFiles(Maps, "*.rthmap"))
        {
            MapData own = MapData.Load(path);
            if (own.IsBlocked(own.SpawnPlane, own.Spawn.X, own.Spawn.Y)) continue; // a region entered only from another
            List<PlacedObject> objects = MapPlacements.Load(path).Objects;
            HashSet<(int, int, int)> reached = Walk(world, own.SpawnPlane, own.Spawn, objects);
            foreach (PlacedObject o in objects)
                if (!Steps.Any(s => reached.Contains((o.Plane, o.X + s.dx, o.Y + s.dy)) && world.EdgeOpen(o.Plane, o.X + s.dx, o.Y + s.dy, -s.dx, -s.dy)))
                    problems.Add(Path.GetFileNameWithoutExtension(path) + ": " + o.Type + " at (" + o.X + ", " + o.Y + ") on plane " + o.Plane);
        }
        Assert.True(problems.Count == 0, "Nobody can stand beside:\n" + string.Join("\n", problems));
    }

    /// <summary>
    /// Everywhere reachable from the spawn within its own region and the regions its climbs lead
    /// to: the open world around a region (the whole continent) doesn't need walking for this.
    /// </summary>
    private static HashSet<(int, int, int)> Walk(WorldMap map, int plane, TileCoord start, List<PlacedObject> objects)
    {
        var reached = new HashSet<(int, int, int)> { (plane, start.X, start.Y) };
        var queue = new Queue<(int p, int x, int y)>(reached);
        var climbs = objects.Where(o => o.Landing != null).ToList();
        var regions = new List<WorldRegion>();
        void Enter(int x, int y)
        {
            WorldRegion? r = map.RegionAt(x, y);
            if (r != null && !regions.Contains(r)) regions.Add(r);
        }
        Enter(start.X, start.Y);
        while (queue.Count > 0)
        {
            (int p, int x, int y) = queue.Dequeue();
            foreach ((int dx, int dy) in Steps)
                if (regions.Any(r => r.Contains(x + dx, y + dy)) && map.CanStep(p, x, y, dx, dy) && reached.Add((p, x + dx, y + dy)))
                    queue.Enqueue((p, x + dx, y + dy));
            foreach (PlacedObject c in climbs)
            {
                if (c.Plane != p || Math.Abs(c.X - x) + Math.Abs(c.Y - y) != 1) continue;
                (int lp, TileCoord l) = c.Landing!.Value;
                Enter(l.X, l.Y);
                if (reached.Add((lp, l.X, l.Y))) queue.Enqueue((lp, l.X, l.Y));
            }
        }
        return reached;
    }
}
