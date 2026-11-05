using System.Net;
using System.Text.Json.Nodes;
using Suwagcms.Sdk.BotRuntime;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.BotRuntime;

public sealed class AvailabilityTests
{
    private static string Message(int number, params string[] articles) =>
        new JsonObject
        {
            ["number"] = number,
            ["off"] = new JsonArray([.. articles.Select(id => (JsonNode)new JsonObject { ["kind"] = "Article", ["id"] = id })]),
        }.ToJsonString();

    private static async Task<HttpStatusCode> SendAsync(BotHost bot, string body)
    {
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await bot.Client.PutAsync(
            new Uri("/suwagcms/availability", UriKind.Relative),
            content,
            TestContext.Current.CancellationToken
        );
        return response.StatusCode;
    }

    [Fact]
    [Trait("criterion", "AC-availability-9")]
    public async Task A_bot_that_was_told_nothing_has_nothing_off()
    {
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());

        var state = await bot.StateAsync();

        Assert.Equal(0, state.Availability.Number);
        Assert.Empty(state.Availability.Off);
        Assert.False(bot.Definitions.IsOff(1, ResourceKind.Article, "cennik"));
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-availability-9")]
    [Trait("criterion", "AC-availability-14")]
    public async Task The_state_and_the_answer_of_the_sdk_show_the_whole_set_of_the_last_message(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));

        Assert.Equal(HttpStatusCode.OK, await SendAsync(bot, Message(1, "cennik")));
        Assert.Equal(HttpStatusCode.OK, await SendAsync(bot, Message(3, "rabaty", "wycena")));

        var state = await bot.StateAsync();
        Assert.Equal(3, state.Availability.Number);
        Assert.Equal(["rabaty", "wycena"], state.Availability.Off.Select(item => item.Id));
        Assert.False(bot.Definitions.IsOff(1, ResourceKind.Article, "cennik"));
        Assert.True(bot.Definitions.IsOff(1, ResourceKind.Article, "rabaty"));
        Assert.False(bot.Definitions.IsOff(1, ResourceKind.Procedure, "rabaty"));
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-availability-10")]
    public async Task The_answer_is_the_same_whichever_version_a_conversation_is_on_and_needs_no_new_version(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        await bot.PutAsync(12, Documents.Make(12));
        await bot.PutAsync(14, Documents.Make(14));

        await SendAsync(bot, Message(1, "cennik"));

        Assert.True(bot.Definitions.IsOff(12, ResourceKind.Article, "cennik"));
        Assert.True(bot.Definitions.IsOff(14, ResourceKind.Article, "cennik"));
        Assert.Equal(14, (await bot.StateAsync()).Current);
        Assert.Equal([12, 14], (await bot.StateAsync()).Held);
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-availability-25")]
    public async Task A_former_id_counts_only_up_to_the_last_version_that_can_hold_it(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        const string message = """
            {"number":1,"off":[{"kind":"Article","id":"priceList"},{"kind":"Article","id":"cennik","upToVersion":12}]}
            """;

        await SendAsync(bot, message);

        Assert.True(bot.Definitions.IsOff(12, ResourceKind.Article, "cennik"));
        Assert.False(bot.Definitions.IsOff(14, ResourceKind.Article, "cennik"));
        Assert.True(bot.Definitions.IsOff(14, ResourceKind.Article, "priceList"));
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-availability-13")]
    public async Task A_resource_that_is_off_is_still_given_by_its_version_and_the_answer_says_it_is_off(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        await bot.PutAsync(1, Documents.WithArticles(1, Documents.Article("cennik", "The prices.")));

        await SendAsync(bot, Message(1, "cennik"));

        Assert.True(bot.Definitions.IsOff(1, ResourceKind.Article, "cennik"));
        var definition = await bot.Definitions.GetAsync(1, TestContext.Current.CancellationToken);
        Assert.Equal("The prices.", definition.Articles.Single(article => article.Id == "cennik").Content);
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-availability-15")]
    public async Task A_lower_number_changes_nothing_and_the_same_message_twice_changes_nothing(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        await SendAsync(bot, Message(5, "cennik"));

        Assert.Equal(HttpStatusCode.OK, await SendAsync(bot, Message(4, "rabaty")));
        Assert.Equal(HttpStatusCode.OK, await SendAsync(bot, Message(5, "cennik")));

        var state = await bot.StateAsync();
        Assert.Equal(5, state.Availability.Number);
        Assert.Equal(["cennik"], state.Availability.Off.Select(item => item.Id));
        Assert.True(bot.Definitions.IsOff(1, ResourceKind.Article, "cennik"));
        Assert.False(bot.Definitions.IsOff(1, ResourceKind.Article, "rabaty"));
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-availability-15")]
    public async Task The_store_itself_keeps_the_higher_number_since_instances_have_no_lock_in_common(string kind)
    {
        using var directory = new TempDirectory();
        var store = Stores.Create(kind, directory);
        var cancellation = TestContext.Current.CancellationToken;

        await store.WriteAvailabilityAsync(
            new BotAvailability { Number = 5, Off = [new OffResource(ResourceKind.Article, "a")] },
            cancellation
        );
        await store.WriteAvailabilityAsync(new BotAvailability { Number = 4, Off = [] }, cancellation);

        Assert.Equal(5, (await store.ReadStateAsync(cancellation)).Availability?.Number);
    }

    [Fact]
    [Trait("criterion", "AC-availability-11")]
    public async Task A_change_delivered_to_one_instance_is_what_the_other_answers_after_its_next_poll()
    {
        var shared = new InMemoryVersionStore();
        await using var a = await BotHost.StartAsync(shared, pollInterval: TimeSpan.FromHours(1));
        await using var b = await BotHost.StartAsync(shared, pollInterval: TimeSpan.FromMilliseconds(50));
        Assert.False(b.Definitions.IsOff(1, ResourceKind.Article, "cennik"));

        await SendAsync(a, Message(1, "cennik"));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!b.Definitions.IsOff(1, ResourceKind.Article, "cennik"))
        {
            await Task.Delay(20, timeout.Token);
        }
        var (onA, onB) = (await a.StateAsync(), await b.StateAsync());
        Assert.Equal(onA.Availability.Number, onB.Availability.Number);
        Assert.Equal(onA.Availability.Off, onB.Availability.Off);
    }

    [Theory]
    [MemberData(nameof(RestartingStores))]
    [Trait("criterion", "AC-availability-12")]
    public async Task After_a_restart_the_bot_reports_the_same_setting(string kind)
    {
        using var directory = new TempDirectory();
        var supplied = new FlakyStore(new InMemoryVersionStore());
        IVersionStore Store()
        {
            return kind == "file" ? new FileVersionStore(directory.Path) : supplied;
        }

        await using (var first = await BotHost.StartAsync(Store()))
        {
            await SendAsync(first, Message(7, "cennik"));
            await first.PutAsync(1, Documents.Make(1));
        }

        await using var restarted = await BotHost.StartAsync(Store());

        Assert.Equal(7, (await restarted.StateAsync()).Availability.Number);
        Assert.True(restarted.Definitions.IsOff(1, ResourceKind.Article, "cennik"));
    }

    public static TheoryData<string> RestartingStores => ["file", "supplied"];

    [Fact]
    [Trait("criterion", "AC-availability-16")]
    public async Task A_store_that_fails_answers_503_and_the_setting_stays()
    {
        var store = new FlakyStore(new InMemoryVersionStore());
        await using var bot = await BotHost.StartAsync(store);
        await SendAsync(bot, Message(1, "cennik"));

        store.FailWrites = true;

        Assert.Equal(HttpStatusCode.ServiceUnavailable, await SendAsync(bot, Message(2)));
        store.FailWrites = false;
        Assert.True(bot.Definitions.IsOff(1, ResourceKind.Article, "cennik"));
        Assert.Equal(1, (await bot.StateAsync()).Availability.Number);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"number":1}""")]
    [InlineData("""{"number":-1,"off":[]}""")]
    [InlineData("""{"number":1,"off":[{"kind":"GlobalFact","id":"a"}]}""")]
    [InlineData("""{"number":1,"off":[{"kind":"Article","id":" "}]}""")]
    [Trait("criterion", "AC-availability-15")]
    public async Task A_message_that_is_not_one_is_refused_and_changes_nothing(string body)
    {
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());
        await SendAsync(bot, Message(1, "cennik"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, await SendAsync(bot, body));

        Assert.True(bot.Definitions.IsOff(1, ResourceKind.Article, "cennik"));
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-article-search-feed-13")]
    public async Task The_feed_marks_an_article_that_is_off_and_gives_it_still(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        await bot.PutAsync(1, Documents.WithArticles(1, Documents.Article("alfa"), Documents.Article("zeta")));

        await SendAsync(bot, Message(1, "zeta"));

        var articles = await bot.Definitions.GetArticlesAsync(1, TestContext.Current.CancellationToken);
        Assert.Equal([("alfa", false), ("zeta", true)], articles.Select(article => (article.Id, article.IsOff)));
    }

    [Fact]
    [Trait("criterion", "AC-article-search-feed-13")]
    public async Task The_bots_code_hears_when_what_is_off_changes_and_nothing_for_a_repeat()
    {
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());
        await bot.PutAsync(1, Documents.WithArticles(1, Documents.Article("alfa")));
        var told = new System.Collections.Concurrent.ConcurrentQueue<int>();
        var second = new TaskCompletionSource();
        bot.Definitions.OnAvailabilityChanged(
            (number, _) =>
            {
                told.Enqueue(number);
                if (number == 2)
                {
                    second.TrySetResult();
                }
                return Task.CompletedTask;
            }
        );

        await SendAsync(bot, Message(1, "alfa"));
        await SendAsync(bot, Message(1, "alfa"));
        await SendAsync(bot, Message(2));

        await second.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.Equal([1, 2], told.Order());
    }

    [Fact]
    [Trait("criterion", "AC-availability-15")]
    public async Task A_refused_message_names_its_code()
    {
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());
        using var content = new StringContent("""{"number":-1,"off":[]}""", System.Text.Encoding.UTF8, "application/json");

        using var response = await bot.Client.PutAsync(
            new Uri("/suwagcms/availability", UriKind.Relative),
            content,
            TestContext.Current.CancellationToken
        );

        Assert.Contains(
            MistakeCodes.AvailabilityInvalid,
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal
        );
    }
}
