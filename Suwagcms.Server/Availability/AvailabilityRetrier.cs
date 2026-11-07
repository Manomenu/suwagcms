using Suwagcms.Server.Bots;
using Suwagcms.Server.Database;

namespace Suwagcms.Server.Availability;

// Why: a bot that was down when something was turned off gets the whole set once a minute until it confirms, with nobody opening a screen; it sleeps while nothing is due. What is pending lives in memory only: after a restart of the CMS the next change, push, send or read of the bot's status repairs it, because trying every bot at start would also reach bots nobody left behind.
internal sealed partial class AvailabilityRetrier(
    Pool pool,
    AvailabilityRetries retries,
    IServiceProvider services,
    TimeProvider time,
    ILogger<AvailabilityRetrier> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var round = retries.Take();
            if (round.Ready.Count > 0)
            {
                await Task.WhenAll(round.Ready.Select(bot => RetryAsync(bot, stoppingToken)));
                continue;
            }
            if (round.Next is not { } next)
            {
                await round.Scheduled.WaitAsync(stoppingToken);
                continue;
            }
            using var woken = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var wait = next - time.GetUtcNow();
            await Task.WhenAny(Task.Delay(wait > TimeSpan.Zero ? wait : TimeSpan.Zero, time, woken.Token), round.Scheduled);
            await woken.CancelAsync();
        }
    }

    private async Task RetryAsync(string bot, CancellationToken cancellationToken)
    {
        try
        {
            // Why: scheduled before the bot is called, so the next try is a minute after this one began, and a confirmation takes it back.
            retries.Schedule(bot);
            if (await BotStore.FindAddressAsync(pool, bot, cancellationToken) is { } address)
            {
                await services.GetRequiredService<AvailabilitySender>().SendAsync(bot, address, cancellationToken);
            }
            else
            {
                retries.Cancel(bot);
            }
        }
        // Why: a database that is briefly away must not stop the retries of every bot, so this one is tried again.
        catch (Npgsql.NpgsqlException exception)
        {
            CouldNotRetry(logger, bot, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Retrying what is turned off for bot {Bot} failed; it is tried again in a minute.")]
    private static partial void CouldNotRetry(ILogger logger, string bot, Exception exception);
}
