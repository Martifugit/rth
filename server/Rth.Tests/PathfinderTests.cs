using Rth.Shared.World;

namespace Rth.Tests;

public class PathfinderTests
{
    private static void AssertWalkable(MapData map, TileCoord start, List<TileCoord> path)
    {
        TileCoord from = start;
        foreach (TileCoord step in path)
        {
            Assert.True(map.CanStep(0, from.X, from.Y, step.X - from.X, step.Y - from.Y), $"Illegal step {from} -> {step}");
            from = step;
        }
    }

    [Fact]
    public void StartEqualsGoal_ReturnsEmptyPath()
    {
        MapData map = Maps.Open(10, 10, new TileCoord(5, 5));
        Assert.Empty(Pathfinder.FindPath(map, 0, new TileCoord(3, 3), new TileCoord(3, 3)));
    }

    [Fact]
    public void OpenGround_StraightLine()
    {
        MapData map = Maps.Open(10, 10, new TileCoord(0, 0));
        List<TileCoord> path = Pathfinder.FindPath(map, 0, new TileCoord(0, 0), new TileCoord(5, 0));
        Assert.Equal(Enumerable.Range(1, 5).Select(x => new TileCoord(x, 0)), path);
    }

    [Fact]
    public void OpenGround_DiagonalsCostOneStep()
    {
        MapData map = Maps.Open(10, 10, new TileCoord(0, 0));
        List<TileCoord> path = Pathfinder.FindPath(map, 0, new TileCoord(0, 0), new TileCoord(3, 3));
        Assert.Equal(3, path.Count);
        Assert.Equal(new TileCoord(3, 3), path[^1]);
    }

    [Fact]
    public void RoutesAroundBlockedTiles()
    {
        MapData map = Maps.FromText(
            "..#...",
            "..#...",
            "..#...",
            "......",
            "..#...");
        var start = new TileCoord(0, 4);
        var goal = new TileCoord(5, 4);
        List<TileCoord> path = Pathfinder.FindPath(map, 0, start, goal);

        Assert.Equal(goal, path[^1]);
        Assert.Contains(new TileCoord(2, 1), path); // the only gap
        AssertWalkable(map, start, path);
    }

    [Fact]
    public void WallEdgesBlockMovementButDoorsLetThrough()
    {
        // A wall along the east edge of column x=2, with a door at y=7.
        MapData map = Maps.Open(10, 10, new TileCoord(0, 0));
        for (int y = 0; y < 10; y++)
            if (y != 7) map.AddWall(0, 2, y, WallSide.East);

        var start = new TileCoord(1, 1);
        var goal = new TileCoord(4, 1);
        List<TileCoord> path = Pathfinder.FindPath(map, 0, start, goal);

        Assert.Equal(goal, path[^1]);
        AssertWalkable(map, start, path);
        Assert.True(path.Count > 3, "Should detour through the door, not go straight.");
        Assert.Contains(path, t => t.Y == 7 && (t.X == 2 || t.X == 3));
    }

    [Fact]
    public void WallsAreStoredOnBothSides()
    {
        MapData map = Maps.Open(5, 5, new TileCoord(0, 0));
        map.AddWall(0, 2, 2, WallSide.North);

        Assert.False(map.CanStep(0, 2, 2, 0, 1));
        Assert.False(map.CanStep(0, 2, 3, 0, -1));
        Assert.True(map.CanStep(0, 2, 2, 1, 0));
    }

    [Fact]
    public void DiagonalStepsCannotCutWallCorners()
    {
        MapData map = Maps.Open(5, 5, new TileCoord(0, 0));
        map.AddWall(0, 1, 1, WallSide.North);

        Assert.False(map.CanStep(0, 1, 1, 1, 1), "Would squeeze past the end of the wall.");
        Assert.False(map.CanStep(0, 1, 1, -1, 1));
        Assert.True(map.CanStep(0, 1, 1, 1, -1));
    }

    [Fact]
    public void DiagonalStepsCannotSqueezeBetweenBlockedTiles()
    {
        MapData map = Maps.FromText(
            "....",
            ".#..",
            "..#.",
            "....");
        // (1,1) to (2,2) would pass between the two blocked tiles at (2,1) and (1,2).
        Assert.False(map.CanStep(0, 1, 1, 1, 1));
    }

    [Fact]
    public void UnreachableGoal_WalksToClosestReachableTile()
    {
        MapData map = Maps.FromText(
            ".......",
            "..###..",
            "..#.#..",
            "..###..",
            ".......");
        var start = new TileCoord(0, 2);
        List<TileCoord> path = Pathfinder.FindPath(map, 0, start, new TileCoord(3, 2)); // inside the box

        Assert.NotEmpty(path);
        Assert.Equal(new TileCoord(1, 2), path[^1]);
        AssertWalkable(map, start, path);
    }

    [Fact]
    public void BlockedGoal_StopsNextToIt()
    {
        MapData map = Maps.FromText(
            ".....",
            "..#..",
            ".....");
        List<TileCoord> path = Pathfinder.FindPath(map, 0, new TileCoord(0, 1), new TileCoord(2, 1));
        Assert.Equal(new TileCoord(1, 1), path[^1]);
    }

    [Fact]
    public void OutsideTheMapIsBlocked()
    {
        MapData map = Maps.Open(3, 3, new TileCoord(0, 0));
        Assert.True(map.IsBlocked(0, -1, 0));
        Assert.True(map.IsBlocked(0, 3, 0));
        Assert.True(map.IsBlocked(1, 0, 0)); // plane that doesn't exist
        Assert.False(map.CanStep(0, 0, 0, -1, 0));
    }
}
