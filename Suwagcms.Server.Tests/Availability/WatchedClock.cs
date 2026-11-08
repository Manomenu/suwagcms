using Microsoft.Extensions.Time.Testing;

namespace Suwagcms.Server.Tests.Availability;

// Why: tells whether a timer is armed and not yet fired, which is how the test sees the retrier asleep until its next try.
internal sealed class WatchedClock(DateTimeOffset start) : FakeTimeProvider(start)
{
    private int armed;

    public bool Waiting => Volatile.Read(ref armed) > 0;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var watched = new Watched(this);
        watched.Inner = base.CreateTimer(
            given =>
            {
                watched.Stop();
                callback(given);
            },
            state,
            dueTime,
            period
        );
        // Why: counted only once the clock holds the timer; counted before, a test could move the clock first and the timer would start its wait after the jump.
        watched.Arm();
        return watched;
    }

    private sealed class Watched(WatchedClock clock) : ITimer
    {
        private const int Fresh = 0;
        private const int Armed = 1;
        private const int Stopped = 2;

        private int phase = Fresh;

        // Why: set right after the clock makes the inner timer, before the wrapper is handed to anyone.
        public ITimer Inner { get; set; } = null!;

        public void Arm()
        {
            if (Interlocked.CompareExchange(ref phase, Armed, Fresh) == Fresh)
            {
                Interlocked.Increment(ref clock.armed);
            }
        }

        public void Stop()
        {
            if (Interlocked.Exchange(ref phase, Stopped) == Armed)
            {
                Interlocked.Decrement(ref clock.armed);
            }
        }

        public bool Change(TimeSpan dueTime, TimeSpan period) => Inner.Change(dueTime, period);

        public void Dispose()
        {
            Stop();
            Inner.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            Stop();
            return Inner.DisposeAsync();
        }
    }
}
