using Microsoft.Extensions.Time.Testing;

namespace Suwagcms.Server.Tests.Availability;

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
        watched.Arm();
        return watched;
    }

    private sealed class Watched(WatchedClock clock) : ITimer
    {
        private const int Fresh = 0;
        private const int Armed = 1;
        private const int Stopped = 2;

        private int phase = Fresh;

        public ITimer Inner
        {
            get => field ?? throw new InvalidOperationException("the clock sets the inner timer before it hands the wrapper out");
            set;
        }

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
