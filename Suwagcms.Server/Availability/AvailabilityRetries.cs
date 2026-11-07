namespace Suwagcms.Server.Availability;

// Why: only which bot is due and when is kept, never a message: each retry reads the whole set anew, so a newer change replaces what was pending.
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

    public void Cancel(string bot)
    {
        lock (gate)
        {
            due.Remove(bot);
        }
    }

    // Why: the task that says a bot was scheduled is taken under the same lock as what is due, so a bot scheduled right after is never missed.
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
