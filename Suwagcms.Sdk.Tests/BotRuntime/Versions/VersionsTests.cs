using System.Net;
using Suwagcms.Sdk.BotRuntime;
using Suwagcms.Sdk.BotRuntime.Versions;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.BotRuntime.Versions;

public sealed class VersionsTests
{
    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-bot-runtime-sdk-5")]
    public async Task A_new_conversation_gets_the_new_number_and_an_earlier_one_keeps_its_own_definition(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        await bot.PutAsync(12, Documents.Make(12, "twelve"));
        var startedOn = bot.Definitions.CurrentNumber;

        var (status, _) = await bot.PutAsync(14, Documents.Make(14, "fourteen"));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(12, startedOn);
        Assert.Equal(14, bot.Definitions.CurrentNumber);
        Assert.Equal(
            "twelve",
            (await bot.Definitions.GetAsync(startedOn.GetValueOrDefault(), TestContext.Current.CancellationToken)).Facts.Single().Name
        );
        Assert.Equal("fourteen", (await bot.Definitions.GetAsync(14, TestContext.Current.CancellationToken)).Facts.Single().Name);
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-bot-runtime-sdk-6")]
    public async Task The_same_number_and_document_again_changes_nothing_and_another_document_is_refused(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        await bot.PutAsync(1, Documents.Make(1, "first"));
        await bot.PutAsync(2, Documents.Make(2));
        var before = BotHost.Describe(await bot.StateAsync());

        var (again, _) = await bot.PutAsync(2, Documents.Make(2));
        var (conflict, body) = await bot.PutAsync(2, Documents.Make(2, "other"));

        Assert.Equal(HttpStatusCode.OK, again);
        Assert.Equal(before, BotHost.Describe(await bot.StateAsync()));
        Assert.Equal(HttpStatusCode.Conflict, conflict);
        Assert.Equal(MistakeCodes.VersionConflict, body.Member("mistakes").Item(0).Member("code").Text());
        Assert.Equal("a", (await bot.Definitions.GetAsync(2, TestContext.Current.CancellationToken)).Facts.Single().Name);
        Assert.Equal(before, BotHost.Describe(await bot.StateAsync()));
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-bot-runtime-sdk-7")]
    public async Task A_lower_number_is_accepted_and_becomes_current(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        await bot.PutAsync(14, Documents.Make(14));

        var (status, _) = await bot.PutAsync(12, Documents.Make(12, "rollback"));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(12, bot.Definitions.CurrentNumber);
        Assert.Equal(12, (await bot.StateAsync()).Current);
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-bot-runtime-sdk-8")]
    public async Task A_version_neither_current_nor_in_use_is_removed_and_the_current_one_never_is(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        foreach (var number in new[] { 1, 2, 3 })
        {
            await bot.PutAsync(number, Documents.Make(number));
        }

        await bot.Definitions.ReportInUseAsync([2], TestContext.Current.CancellationToken);

        Assert.Equal([2, 3], (await bot.StateAsync()).Held);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => bot.Definitions.GetAsync(1, TestContext.Current.CancellationToken));
        Assert.NotNull(await bot.Definitions.GetAsync(2, TestContext.Current.CancellationToken));

        await bot.Definitions.ReportInUseAsync([], TestContext.Current.CancellationToken);

        var state = await bot.StateAsync();
        Assert.Equal([3], state.Held);
        Assert.Equal(3, state.Current);
        Assert.NotNull(await bot.Definitions.GetAsync(3, TestContext.Current.CancellationToken));
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-bot-runtime-sdk-9")]
    public async Task Until_the_bot_reports_what_is_in_use_nothing_is_removed(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        foreach (var number in new[] { 1, 2, 3 })
        {
            await bot.PutAsync(number, Documents.Make(number));
        }

        Assert.Equal([1, 2, 3], (await bot.StateAsync()).Held);
    }

    [Fact]
    [Trait("criterion", "AC-bot-runtime-sdk-15")]
    public async Task One_scenario_gives_the_same_state_on_the_file_store_and_on_a_store_supplied_by_a_test()
    {
        using var directory = new TempDirectory();
        IVersionStore[] stores = [new FileVersionStore(directory.Path), new FlakyStore(new InMemoryVersionStore())];

        var states = new[] { await ScenarioAsync(stores[0]), await ScenarioAsync(stores[1]) };

        Assert.Equal(BotHost.Describe(states[0]), BotHost.Describe(states[1]));
        Assert.Equal([2, 3], states[0].Held);
        Assert.Equal(3, states[0].Current);
    }

    private static async Task<BotState> ScenarioAsync(IVersionStore store)
    {
        await using var bot = await BotHost.StartAsync(store);
        await bot.PutAsync(1, Documents.Make(1));
        await bot.PutAsync(2, Documents.Make(2));
        await bot.PutAsync(2, Documents.Make(2));
        await bot.PutAsync(3, Documents.Make(3));
        await bot.PutAsync(1, Documents.Make(1, "other"));
        await bot.Definitions.ReportInUseAsync([2], TestContext.Current.CancellationToken);
        return await bot.StateAsync();
    }
}
