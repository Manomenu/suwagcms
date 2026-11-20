namespace Suwagcms.Server.Availability;

internal sealed class AvailabilityRetries(TimeProvider time)
{
    public static readonly TimeSpan Every = TimeSpan.FromMinutes(1);

    private readonly Lock gate = new();
    private readonly Dictionary<string, DateTimeOffset> due = new(StringComparer.Ordinal);
    private TaskCompletionSource scheduled = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal sealed record Round(IReadOnlyList<string> Ready, DateTimeOffset? Next, Task Scheduled);

    public void Schedule(string bot)
    {
        lock (gate)
        {
            if (due.TryAdd(bot, time.GetUtcNow() + Every))
            {
                scheduled.TrySetResult();
                scheduled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
    }

    public bool Idle
    {
        get
        {
            lock (gate)
            {
                return due.Count == 0;
            }
        }
    }

    public void Cancel(string bot)
    {
        lock (gate)
        {
            due.Remove(bot);
        }
    }

    public Round Take()
    {
        lock (gate)
        {
            var now = time.GetUtcNow();
            List<string> ready = [.. due.Where(item => item.Value <= now).Select(item => item.Key)];
            foreach (var bot in ready)
            {
                due.Remove(bot);
            }
            return new Round(ready, due.Count == 0 ? null : due.Values.Min(), scheduled.Task);
        }
    }
}
