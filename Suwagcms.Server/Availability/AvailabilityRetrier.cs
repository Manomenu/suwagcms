using Npgsql;
using Suwagcms.Server.Bots;

namespace Suwagcms.Server.Availability;

internal sealed partial class AvailabilityRetrier(
    NpgsqlDataSource dataSource,
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
            retries.Schedule(bot);
            if (await BotStore.FindAddressAsync(dataSource, bot, cancellationToken) is { } address)
            {
                await services.GetRequiredService<AvailabilitySender>().SendAsync(bot, address, cancellationToken);
            }
            else
            {
                retries.Cancel(bot);
            }
        }
        catch (Npgsql.NpgsqlException exception)
        {
            CouldNotRetry(logger, bot, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Retrying what is turned off for bot {Bot} failed; it is tried again in a minute.")]
    private static partial void CouldNotRetry(ILogger logger, string bot, Exception exception);
}
