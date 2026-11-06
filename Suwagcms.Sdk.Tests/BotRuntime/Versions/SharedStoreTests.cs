using Suwagcms.Sdk.BotRuntime.Versions;

namespace Suwagcms.Sdk.Tests.BotRuntime.Versions;

public sealed class SharedStoreTests
{
    [Fact]
    [Trait("criterion", "AC-bot-runtime-sdk-10")]
    public async Task After_a_restart_the_file_store_gives_the_same_version_and_every_definition_held()
    {
        using var directory = new TempDirectory();
        await using (var first = await BotHost.StartAsync(new FileVersionStore(directory.Path)))
        {
            await first.PutAsync(1, Documents.Make(1, "one"));
            await first.PutAsync(2, Documents.Make(2, "two"));
        }

        await using var restarted = await BotHost.StartAsync(new FileVersionStore(directory.Path));

        Assert.Equal(2, restarted.Definitions.CurrentNumber);
        var state = await restarted.StateAsync();
        Assert.Equal(2, state.Current);
        Assert.Equal([1, 2], state.Held);
        Assert.Equal("one", (await restarted.Definitions.GetAsync(1, TestContext.Current.CancellationToken)).Facts.Single().Name);
    }

    [Fact]
    [Trait("criterion", "AC-bot-runtime-sdk-11")]
    public async Task A_version_pushed_to_one_instance_becomes_current_on_the_other_at_its_next_poll()
    {
        var shared = new InMemoryVersionStore();
        await using var a = await BotHost.StartAsync(shared, pollInterval: TimeSpan.FromHours(1));
        await using var b = await BotHost.StartAsync(shared, pollInterval: TimeSpan.FromMilliseconds(50));
        Assert.Null(b.Definitions.CurrentNumber);

        await a.PutAsync(14, Documents.Make(14));

        await WaitAsync(() => b.Definitions.CurrentNumber == 14);
        Assert.Equal(BotHost.Describe(await a.StateAsync()), BotHost.Describe(await b.StateAsync()));
        Assert.Equal(14, (await b.StateAsync()).Current);
    }

    [Fact]
    [Trait("criterion", "AC-bot-runtime-sdk-12")]
    public async Task A_conversation_started_on_one_instance_gets_its_definition_on_another_from_the_shared_store()
    {
        var shared = new InMemoryVersionStore();
        await using var a = await BotHost.StartAsync(shared);
        await using var b = await BotHost.StartAsync(shared);
        await a.PutAsync(12, Documents.Make(12, "twelve"));
        var startedOn = a.Definitions.CurrentNumber.GetValueOrDefault();
        await a.PutAsync(14, Documents.Make(14, "fourteen"));

        var definition = await b.Definitions.GetAsync(startedOn, TestContext.Current.CancellationToken);

        Assert.Equal("twelve", definition.Facts.Single().Name);
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }
}
