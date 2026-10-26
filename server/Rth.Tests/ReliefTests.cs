using Rth.Shared.World.Layout;

namespace Rth.Tests;

/// <summary>Landforms (<see cref="Relief"/>) as the layout's terrain reads them.</summary>
public class ReliefTests
{
    private static Relief OneScarp() => new Relief(new ContinentData
    {
        Seed = 1,
        Relief =
        {
            new ReliefDef { Id = "edge", Kind = "scarp", Height = 2f, Edge = 5f, Side = "right", Reach = 45f,
                            Line = new List<float[]> { new[] { 0f, 0f }, new[] { 100f, 0f } } },
        },
    });

    [Fact]
    public void AScarpRaisesItsHighSideAlongItsLine()
    {
        Relief relief = OneScarp();
        Assert.InRange(relief.At(50f, -20f), 1.8f, 2.01f); // right of a line running east: the south
        Assert.InRange(relief.At(50f, 20f), -0.01f, 0.2f);
    }

    [Fact]
    public void AScarpDiesAwayPastTheEndsOfItsLineInsteadOfStoppingAtACliff()
    {
        // It used to go on along the line's extension until its bounding box cut it off.
        Relief relief = OneScarp();
        Assert.Equal(0f, relief.At(-Relief.ScarpTaper - 1f, -20f), 3);
        Assert.Equal(0f, relief.At(100f + Relief.ScarpTaper + 1f, -20f), 3);
        // No step anywhere past the west end: a tile apart, the land differs by little.
        for (float x = -110f; x < 0f; x += 1f)
            Assert.True(Math.Abs(relief.At(x, -20f) - relief.At(x + 1f, -20f)) < 0.2f, $"a step at x {x}");
    }
}
