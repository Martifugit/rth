using Rth.Shared.World;

namespace Rth.Tests;

public class HeightfieldTests
{
    [Fact]
    public void SamplesMeetTheCornersAndFollowTheDrawnTriangles()
    {
        var field = new Heightfield(10, 20, 2, 2);
        field.SetCorner(11, 21, 2f); // the middle corner up: a little peak

        Assert.Equal(0f, field.Sample(10f, 20f), 4);
        Assert.Equal(2f, field.Sample(11f, 21f), 4);
        Assert.Equal(1f, field.Sample(10.5f, 20.5f), 4);   // on the diagonal, halfway up
        // Tile (10, 20): the south-east triangle (u >= v) climbs only towards the north-east corner.
        Assert.Equal(0.5f, field.Sample(10.5f, 20.25f), 4);
        Assert.Equal(0.5f, field.Sample(10.25f, 20.5f), 4);
    }

    [Fact]
    public void PointsOutsideTakeTheNearestEdge()
    {
        var field = new Heightfield(0, 0, 1, 1, new[] { 0f, 1f, 0f, 1f });
        Assert.Equal(1f, field.Sample(5f, 0.5f), 4);
        Assert.Equal(0f, field.Sample(-3f, 0.5f), 4);
        Assert.False(field.Contains(5f, 0.5f));
    }

    [Fact]
    public void SteepTilesAreCliffs()
    {
        var field = new Heightfield(0, 0, 3, 1);
        field.Fill((x, y) => x >= 2 ? 2f : x == 1 ? 0.5f : 0f); // a gentle step, then a 1.5-tile face

        Assert.False(field.IsSteep(0, 0, 1f));
        Assert.True(field.IsSteep(1, 0, 1f));
        Assert.False(field.IsSteep(2, 0, 1f));
        Assert.Equal(1.5f, field.Rise(1, 0), 4);
    }

    [Fact]
    public void NoiseIsRepeatableAndStaysInRange()
    {
        float min = float.MaxValue, max = float.MinValue;
        for (int y = -50; y < 50; y++)
            for (int x = -50; x < 50; x++)
            {
                float h = Heightfield.Noise(x, y, seed: 7);
                Assert.Equal(h, Heightfield.Noise(x, y, seed: 7));
                min = Math.Min(min, h);
                max = Math.Max(max, h);
            }
        Assert.InRange(min, 0.6f, 3.75f);
        Assert.InRange(max, 0.6f, 3.75f);
        Assert.True(max - min > 1f, "noise should roll, not sit flat");
    }

    [Fact]
    public void NoiseRollsGently()
    {
        // The 2004 world's neighbouring tiles differ by a median of 0.06; base noise should be that soft.
        var steps = new List<float>();
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                steps.Add(Math.Abs(Heightfield.Noise(x + 1, y, 3) - Heightfield.Noise(x, y, 3)));
        steps.Sort();
        Assert.True(steps[steps.Count / 2] < 0.15f, $"median step {steps[steps.Count / 2]}");
        Assert.True(steps[^1] < 1f, "base noise must never make a cliff");
    }

    [Fact]
    public void RejectsCornerArraysOfTheWrongSize()
    {
        Assert.Throws<ArgumentException>(() => new Heightfield(0, 0, 2, 2, new float[4]));
    }
}
