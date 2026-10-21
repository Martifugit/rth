using System.Diagnostics;
using Rth.Server.Timing;

namespace Rth.Tests;

/// <summary>Measures wall-clock time, so it runs alone: heavy simulations beside it skew the clock.</summary>
[CollectionDefinition("RealTime", DisableParallelization = true)]
public sealed class RealTimeCollection { }

[Collection("RealTime")]
public class TickTimingTests
{
    private static readonly long Period = TimeSpan.FromMilliseconds(600).Ticks;

    [Fact]
    public void TicksStayOnTheGridWhateverTheWorkTakes()
    {
        var clock = new FakeClock { Now = 12345 };
        long start = clock.Now;
        var random = new Random(1);
        var tickStarts = new List<long>();

        var loop = new TickLoop(() =>
        {
            tickStarts.Add(clock.Now);
            clock.Now += random.Next(0, (int)(Period * 0.9)); // simulated work, up to 90% of a tick
        }, TimeSpan.FromTicks(Period), clock, _ => { });
        loop.Run(1000);

        Assert.Equal(1000, tickStarts.Count);
        for (int n = 0; n < tickStarts.Count; n++)
            Assert.Equal(start + n * Period, tickStarts[n]);
    }

    [Fact]
    public void AnOverrunningTickIsFollowedImmediatelyThenTheGridResumes()
    {
        var schedule = new TickSchedule(Period, start: 0);

        // Tick 0 overran: it finished 1.5 ticks in. Tick 1 (due at 1.0) should run right away.
        Assert.Equal(0, schedule.Advance(Period * 3 / 2));
        // Tick 1 finished quickly at 1.6; tick 2 is due at exactly 2.0.
        Assert.Equal(Period * 2 - Period * 16 / 10, schedule.Advance(Period * 16 / 10));
        Assert.Equal(Period * 2, schedule.CurrentDeadline);
        Assert.Equal(0, schedule.SkippedTicks);
    }

    [Fact]
    public void ALongStallSkipsTheBacklogAndRejoinsTheGrid()
    {
        var schedule = new TickSchedule(Period, start: 0);

        // Tick 0 took 20.3 ticks (say the machine slept). Don't replay 20 ticks back to back.
        long now = Period * 203 / 10;
        long wait = schedule.Advance(now);

        Assert.Equal(Period * 21, schedule.CurrentDeadline);
        Assert.Equal(Period * 21 - now, wait);
        Assert.Equal(20, schedule.SkippedTicks);
    }

    [Fact]
    public void RealTimeLoopKeepsItsAverageRate()
    {
        // Real clock, fast ticks. Generous bounds: this checks drift, not OS sleep precision.
        const int periodMs = 20;
        var starts = new List<long>();
        var stopwatch = Stopwatch.StartNew();
        var loop = new TickLoop(() =>
        {
            lock (starts) starts.Add(stopwatch.ElapsedTicks);
            Thread.Sleep(5);
        }, TimeSpan.FromMilliseconds(periodMs), new SystemClock(), _ => { });

        loop.Start();
        Thread.Sleep(1000);
        loop.Stop();

        int count;
        double lastStartMs;
        lock (starts)
        {
            count = starts.Count;
            lastStartMs = (starts[^1] - starts[0]) * 1000.0 / Stopwatch.Frequency;
        }
        // Ticks should keep landing on the 20 ms grid rather than slipping by 5 ms of work each time.
        double expected = (count - 1) * periodMs;
        Assert.InRange(count, 40, 52);
        Assert.InRange(lastStartMs, expected - periodMs, expected + periodMs);
    }
}
