using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Suwagcms.Contracts.Availability;
using Suwagcms.Server.Availability;

namespace Suwagcms.Server.Tests.Availability;

// Why: the retrier's clock is a fake one here, so a minute passes when the test says so and a retry that should not happen cannot hide behind a slow run. Only the retrier runs on it: a bot call in flight keeps its real timeout, so moving the clock never cuts one short.
public sealed class AvailabilityRetryTests(TestDatabase database, SharedApp shared) : IClassFixture<SharedApp>
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    private TestApp App => shared.App;
    private readonly WatchedClock clock = new(TimeProvider.System.GetUtcNow());
    private AvailabilityRetries? retries;
    private int puts;

    private async Task<AvailabilityScene> SceneAsync()
    {
        _ = database.Pool;
        retries = new AvailabilityRetries(clock);
        var factory = App.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton(retries));
                services.Remove(services.Single(service => service.ImplementationType == typeof(AvailabilityRetrier)));
                services.AddHostedService(provider => ActivatorUtilities.CreateInstance<AvailabilityRetrier>(provider, clock));
            })
        );
        return await AvailabilityScene.StartAsync(factory.CreateClient());
    }

    // Why: the bot refuses the first `refusals` messages it is sent and takes the rest, and every message is counted.
    private void Refuse(AvailabilityScene scene, int refusals) =>
        scene.Bot.OnPush = context =>
        {
            if (Interlocked.Increment(ref puts) <= refusals)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Abort();
            }
            return Task.CompletedTask;
        };

    private int Puts => Volatile.Read(ref puts);

    // Why: the deadline only stops a broken run from hanging; what is waited for is certain to come, however slow the machine.
    private static async Task Until(Func<Task<bool>> done)
    {
        var deadline = TimeProvider.System.GetUtcNow().AddSeconds(60);
        while (!await done())
        {
            Assert.True(TimeProvider.System.GetUtcNow() < deadline, "what the test waited for did not happen within 60 seconds");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private Task UntilPuts(int count) => Until(() => Task.FromResult(Puts >= count));

    // Why: at rest the retrier is either waiting on its timer or has nothing due; in between it may be reading the clock, and a jump then would move its next try.
    private Task Settled() => Until(() => Task.FromResult(clock.Waiting || retries!.Idle));

    private async Task Pass(TimeSpan passing)
    {
        await Settled();
        clock.Advance(passing);
    }

    // Why: a retry can only start from a timer that fired, so once the retrier is at rest again after the clock moved, a put that has not come never will.
    private async Task NoPutAfter(TimeSpan passing)
    {
        await Settled();
        var before = Puts;
        clock.Advance(passing);
        await Settled();
        Assert.Equal(before, Puts);
    }

    [Fact]
    [Trait("criterion", "AC-availability-33")]
    public async Task A_message_the_bot_refuses_is_sent_again_after_each_minute_until_the_bot_takes_it()
    {
        await using var scene = await SceneAsync();
        Refuse(scene, refusals: 2);

        Assert.Equal(AvailabilityDeliveryState.NotDelivered, (await scene.Turn("off")).Delivery.State);
        Assert.Equal(1, Puts);

        await NoPutAfter(Minute - TimeSpan.FromSeconds(1));
        await Pass(TimeSpan.FromSeconds(1));
        await UntilPuts(2);
        await NoPutAfter(Minute - TimeSpan.FromSeconds(1));
        await Pass(TimeSpan.FromSeconds(1));
        await UntilPuts(3);

        await Until(async () => (await scene.OnBot()).Number == 1);
        Assert.Equal(AvailabilityDeliveryState.Confirmed, (await scene.View()).Delivery.State);
    }

    [Fact]
    [Trait("criterion", "AC-availability-34")]
    public async Task Once_the_bot_takes_the_message_or_when_nothing_failed_nothing_more_is_sent()
    {
        await using var scene = await SceneAsync();
        Refuse(scene, refusals: 0);
        Assert.Equal(AvailabilityDeliveryState.Confirmed, (await scene.Turn("off")).Delivery.State);
        await NoPutAfter(Minute);
        await NoPutAfter(Minute);

        Refuse(scene, refusals: Puts + 1);
        Assert.Equal(AvailabilityDeliveryState.NotDelivered, (await scene.Turn("on")).Delivery.State);
        var refused = Puts;
        await Pass(Minute);
        await UntilPuts(refused + 1);
        await Until(async () => (await scene.OnBot()).Number == 2);

        await NoPutAfter(Minute);
        await NoPutAfter(Minute);
        await NoPutAfter(Minute * 5);
    }

    [Fact]
    [Trait("criterion", "AC-availability-35")]
    public async Task A_newer_change_replaces_the_pending_message_so_one_retry_carries_the_whole_latest_set()
    {
        await using var scene = await SceneAsync();
        Refuse(scene, refusals: 2);
        Assert.Equal(AvailabilityDeliveryState.NotDelivered, (await scene.Turn("off")).Delivery.State);
        await Pass(TimeSpan.FromSeconds(30));
        Assert.Equal(AvailabilityDeliveryState.NotDelivered, (await scene.Turn("off", id: AvailabilityScene.Unused)).Delivery.State);

        await Pass(TimeSpan.FromSeconds(30));
        await UntilPuts(3);
        await Settled();

        var onBot = await scene.OnBot();
        Assert.Equal(2, onBot.Number);
        Assert.Equal([AvailabilityScene.Used, AvailabilityScene.Unused], onBot.Off.Select(item => item.Id).Order(StringComparer.Ordinal));
        await NoPutAfter(Minute);
    }
}
