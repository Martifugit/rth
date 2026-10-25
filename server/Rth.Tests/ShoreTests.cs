using Rth.Shared.World.Ground;

namespace Rth.Tests;

/// <summary>How a painted ground's shores are drawn (shared/World/Ground/Shore.cs).</summary>
public class ShoreTests
{
    [Fact]
    public void TheGroundDipsMoreTheFurtherItIsUnderWater()
    {
        // 7 by 7 tiles of land (0) with 5 by 5 of water (1) in the middle.
        const int n = 7;
        var kinds = new int[n * n];
        for (int y = 1; y < 6; y++)
            for (int x = 1; x < 6; x++) kinds[y * n + x] = 1;
        Shore shore = Shore.Of(kinds, n, n, new[] { false, true });

        Assert.False(shore.Wet(0, 0));
        Assert.Equal(0f, shore.Dip(0, 0)); // dry land keeps its height
        Assert.True(shore.Wet(1, 1));
        Assert.Equal(0, shore.Distance(1, 1)); // on the waterline: touches land and water
        Assert.InRange(shore.Wetness(1, 3), 0.4f, 0.6f); // a straight shore: about half water
        Assert.InRange(shore.Dip(1, 3), 0.1f, 0.25f); // dips about as deep as the water's surface
        Assert.True(shore.Wetness(1, 1) < shore.Wetness(1, 3)); // a corner of the lake: less water round it, so rounded
        Assert.Equal(1, shore.Distance(2, 3));
        Assert.Equal(2, shore.Distance(3, 3)); // the middle
        Assert.True(shore.Dip(3, 3) > shore.Dip(2, 3) && shore.Dip(2, 3) > shore.Dip(1, 3));
        Assert.Equal(1f, shore.Shallow(1, 3));
        Assert.True(shore.Shallow(3, 3) < shore.Shallow(2, 3));
        Assert.Equal(1, shore.WaterAround(1, 1));
        Assert.Equal(-1, shore.WaterAround(0, 0));
    }
}
