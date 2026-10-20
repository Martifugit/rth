using System;
using System.Threading;

namespace Rth.Server.Timing
{
    /// <summary>Runs a tick action on its own thread on a drift-corrected fixed period.</summary>
    public sealed class TickLoop
    {
        private readonly Action _tick;
        private readonly long _period;
        private readonly IClock _clock;
        private readonly Action<string> _log;
        private readonly ManualResetEvent _stop = new ManualResetEvent(false);
        private Thread? _thread;
        private long _ticksRun;

        public TickLoop(Action tick, TimeSpan period, IClock clock, Action<string> log)
        {
            if (period <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(period));
            _tick = tick;
            _period = period.Ticks;
            _clock = clock;
            _log = log;
        }

        public long TicksRun => Interlocked.Read(ref _ticksRun);

        public void Start()
        {
            if (_thread != null) throw new InvalidOperationException("Tick loop already started.");
            _stop.Reset();
            _thread = new Thread(() => Run(long.MaxValue)) { IsBackground = true, Name = "Rth tick loop" };
            _thread.Start();
        }

        /// <summary>Signals the loop to stop and waits for the current tick to finish.</summary>
        public void Stop()
        {
            _stop.Set();
            _thread?.Join();
            _thread = null;
        }

        /// <summary>Runs on the calling thread until stopped or <paramref name="maxTicks"/> have run.</summary>
        public void Run(long maxTicks)
        {
            var schedule = new TickSchedule(_period, _clock.Now);
            for (long n = 0; n < maxTicks; n++)
            {
                try
                {
                    _tick();
                }
                catch (Exception e)
                {
                    // One bad tick must not take the world down; log it and keep the schedule.
                    _log("Tick failed: " + e);
                }
                Interlocked.Increment(ref _ticksRun);

                long skippedBefore = schedule.SkippedTicks;
                long wait = schedule.Advance(_clock.Now);
                if (schedule.SkippedTicks != skippedBefore)
                    _log("Tick loop fell behind; skipped " + (schedule.SkippedTicks - skippedBefore) + " ticks.");
                if (_clock.Wait(wait, _stop)) return;
            }
        }
    }
}
