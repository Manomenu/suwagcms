using System.Net;
using Suwagcms.Sdk.BotRuntime.Versions;

namespace Suwagcms.Sdk.Tests.BotRuntime.Versions;

public sealed class MakeCurrentTests
{
    private static string Digest(int number, string label = "a") => Documents.Make(number, label).Member("digest").Text();

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-version-rollback-7")]
    public async Task A_held_version_becomes_current_and_repeating_the_request_changes_nothing(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        await bot.PutAsync(12, Documents.Make(12, "twelve"));
        await bot.PutAsync(14, Documents.Make(14, "fourteen"));

        var (status, body) = await bot.MakeCurrentAsync(12, Digest(12, "twelve"));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(12, body.Member("current").GetValue<int>());
        Assert.Equal(12, bot.Definitions.CurrentNumber);
        var after = BotHost.Describe(await bot.StateAsync());
        Assert.Contains("|12|12,14|", after, StringComparison.Ordinal);

        var (again, _) = await bot.MakeCurrentAsync(12, Digest(12, "twelve"));

        Assert.Equal(HttpStatusCode.OK, again);
        Assert.Equal(after, BotHost.Describe(await bot.StateAsync()));
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-version-rollback-7")]
    public async Task A_number_not_held_or_a_digest_of_another_document_is_refused_and_the_state_stays(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        await bot.PutAsync(12, Documents.Make(12, "twelve"));
        await bot.PutAsync(14, Documents.Make(14, "fourteen"));
        var before = BotHost.Describe(await bot.StateAsync());

        var (missing, missingBody) = await bot.MakeCurrentAsync(13, Digest(13));
        var (other, otherBody) = await bot.MakeCurrentAsync(12, Digest(12, "another"));

        Assert.Equal(HttpStatusCode.Conflict, missing);
        Assert.Equal("version.notHeld", missingBody.Member("mistakes").Item(0).Member("code").Text());
        Assert.Equal(HttpStatusCode.Conflict, other);
        Assert.Equal("version.conflict", otherBody.Member("mistakes").Item(0).Member("code").Text());
        Assert.Equal(before, BotHost.Describe(await bot.StateAsync()));
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-7")]
    public async Task A_body_that_names_no_version_is_refused_and_the_state_stays()
    {
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());
        await bot.PutAsync(1, Documents.Make(1));
        using var content = new StringContent("{\"number\":1}", System.Text.Encoding.UTF8, "application/json");

        using var response = await bot.Client.PutAsync(
            new Uri("/suwagcms/current", UriKind.Relative),
            content,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(1, bot.Definitions.CurrentNumber);
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-version-rollback-8")]
    public async Task A_held_version_that_no_longer_validates_is_refused_with_every_mistake_and_the_state_stays(string kind)
    {
        using var directory = new TempDirectory();
        var store = Stores.Create(kind, directory);
        await using var bot = await BotHost.StartAsync(store);
        await bot.PutAsync(14, Documents.Make(14));
        var stale = Documents.Make(12);
        stale["facts"] = new System.Text.Json.Nodes.JsonArray(
            Documents.Fact("x"),
            Documents.Fact("x"),
            Documents.Fact("y"),
            Documents.Fact("y")
        );
        Documents.Sign(stale);
        await store.WriteVersionAsync(12, stale.ToJsonString(), TestContext.Current.CancellationToken);
        await store.WriteCurrentAsync(14, TestContext.Current.CancellationToken);
        var before = BotHost.Describe(await bot.StateAsync());

        var (status, body) = await bot.MakeCurrentAsync(12, stale.Member("digest").Text());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Equal(2, body.Member("mistakes").Items().Count(mistake => mistake.Member("code").Text() == "resource.duplicateId"));
        Assert.Equal(before, BotHost.Describe(await bot.StateAsync()));
        Assert.Equal(14, bot.Definitions.CurrentNumber);
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-7")]
    public async Task A_store_that_fails_during_the_write_answers_with_the_reason_and_the_state_stays()
    {
        var store = new FlakyStore(new InMemoryVersionStore());
        await using var bot = await BotHost.StartAsync(store);
        await bot.PutAsync(12, Documents.Make(12, "twelve"));
        await bot.PutAsync(14, Documents.Make(14, "fourteen"));
        store.FailWrites = true;

        var (status, body) = await bot.MakeCurrentAsync(12, Digest(12, "twelve"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Contains("the store is down", body.Member("detail").Text(), StringComparison.Ordinal);
        Assert.Equal(14, (await bot.StateAsync()).Current);
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-version-rollback-6")]
    public async Task After_going_back_a_new_conversation_gets_the_chosen_version_and_an_earlier_one_keeps_its_own(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        await bot.PutAsync(12, Documents.Make(12, "twelve"));
        await bot.PutAsync(14, Documents.Make(14, "fourteen"));
        var startedOn = bot.Definitions.CurrentNumber.GetValueOrDefault();
        await bot.Definitions.ReportInUseAsync([12, startedOn], TestContext.Current.CancellationToken);

        await bot.MakeCurrentAsync(12, Digest(12, "twelve"));

        Assert.Equal(12, bot.Definitions.CurrentNumber);
        Assert.Equal("fourteen", (await bot.Definitions.GetAsync(startedOn, TestContext.Current.CancellationToken)).Facts.Single().Name);
        Assert.Equal("twelve", (await bot.Definitions.GetAsync(12, TestContext.Current.CancellationToken)).Facts.Single().Name);
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-7")]
    public async Task The_version_left_goes_unless_it_is_reported_as_in_use()
    {
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());
        await bot.PutAsync(12, Documents.Make(12, "twelve"));
        await bot.Definitions.ReportInUseAsync([12], TestContext.Current.CancellationToken);
        await bot.PutAsync(14, Documents.Make(14, "fourteen"));

        await bot.MakeCurrentAsync(12, Digest(12, "twelve"));

        Assert.Equal([12], (await bot.StateAsync()).Held);
    }

    [Fact]
    [Trait("criterion", "AC-version-rollback-10")]
    public async Task A_held_version_made_current_through_one_instance_is_current_on_the_other_at_its_next_poll()
    {
        var shared = new InMemoryVersionStore();
        await using var a = await BotHost.StartAsync(shared, pollInterval: TimeSpan.FromHours(1));
        await using var b = await BotHost.StartAsync(shared, pollInterval: TimeSpan.FromMilliseconds(50));
        await a.PutAsync(12, Documents.Make(12, "twelve"));
        await a.PutAsync(14, Documents.Make(14, "fourteen"));
        await WaitAsync(() => b.Definitions.CurrentNumber == 14);

        await a.MakeCurrentAsync(12, Digest(12, "twelve"));

        await WaitAsync(() => b.Definitions.CurrentNumber == 12);
        Assert.Equal(BotHost.Describe(await a.StateAsync()), BotHost.Describe(await b.StateAsync()));
        Assert.Equal(12, (await b.StateAsync()).Current);
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
