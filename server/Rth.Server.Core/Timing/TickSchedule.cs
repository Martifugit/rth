namespace Rth.Server.Timing
{
    /// <summary>
    /// Keeps ticks on a fixed grid (start + n * period) no matter how long each tick's work takes,
    /// so timing errors never accumulate. A tick that overruns makes the next one start immediately;
    /// if the server falls more than <see cref="MaxBehindTicks"/> behind (e.g. after the machine
    /// slept), the backlog is dropped and the schedule rejoins the grid at the next grid point.
    /// </summary>
    public sealed class TickSchedule
    {
        public const int MaxBehindTicks = 5;

        private readonly long _start;
        private readonly long _period;
        private long _index;

        public TickSchedule(long period, long start)
        {
            _period = period;
            _start = start;
        }

        /// <summary>When the current tick is due to start.</summary>
        public long CurrentDeadline => _start + _index * _period;

        /// <summary>Ticks skipped because the server fell too far behind.</summary>
        public long SkippedTicks { get; private set; }

        /// <summary>Moves on to the next tick and returns how long to wait before running it (0 if it is already due).</summary>
        public long Advance(long now)
        {
            _index++;
            long behind = now - CurrentDeadline;
            if (behind > MaxBehindTicks * _period)
            {
                // Rejoin the grid at the first grid point at or after now.
                long target = (now - _start + _period - 1) / _period;
                SkippedTicks += target - _index;
                _index = target;
            }
            long wait = CurrentDeadline - now;
            return wait > 0 ? wait : 0;
        }
    }
}
