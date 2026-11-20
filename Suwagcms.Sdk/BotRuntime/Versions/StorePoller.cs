using Microsoft.Extensions.Hosting;

namespace Suwagcms.Sdk.BotRuntime.Versions;

internal sealed class StorePoller(VersionRuntime runtime, TimeSpan interval) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await runtime.RefreshAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await runtime.RefreshAsync(stoppingToken);
        }
    }
}
