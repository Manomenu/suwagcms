using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Logging;
using Suwagcms.Sdk.BotRuntime;

namespace Suwagcms.Sdk.Tests.BotRuntime;

public sealed class ArticleFeedTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ConcurrentQueue<int> Listen(IBotDefinitions definitions)
    {
        var told = new ConcurrentQueue<int>();
        definitions.OnCurrentChanged(
            (number, _) =>
            {
                told.Enqueue(number);
                return Task.CompletedTask;
            }
        );
        return told;
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-article-search-feed-1")]
    [Trait("criterion", "AC-article-search-feed-11")]
    public async Task A_version_gives_every_article_in_a_fixed_order_with_its_fields_and_its_base_language(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        await bot.PutAsync(1, Documents.WithArticles(1, Documents.Article("zeta"), Documents.Article("alfa")));

        var articles = await bot.Definitions.GetArticlesAsync(1, Token);

        Assert.Equal(["alfa", "zeta"], articles.Select(article => article.Id));
        var first = articles[0];
        Assert.Equal(
            ("faq", "pl", "Title of alfa", "What alfa answers", "Text.", "Guide of alfa"),
            (first.KnowledgeBase, first.Language, first.Title, first.Description, first.Content, first.LlmGuide)
        );
    }

    [Fact]
    [Trait("criterion", "AC-article-search-feed-2")]
    public async Task Content_and_the_guide_are_byte_identical_to_the_document_with_their_links()
    {
        const string content = "See  [prices](cms://article/other)\n\n* `as is` ✓ ";
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());
        var article = Documents.Article("linked", content);
        article["llmGuide"] = "Go to [the other](cms://article/other)  first.";
        await bot.PutAsync(1, Documents.WithArticles(1, article, Documents.Article("other")));

        var fed = (await bot.Definitions.GetArticlesAsync(1, Token))[0];

        Assert.Equal(content, fed.Content);
        Assert.Equal("Go to [the other](cms://article/other)  first.", fed.LlmGuide);
    }

    [Fact]
    [Trait("criterion", "AC-article-search-feed-3")]
    public async Task An_earlier_held_version_still_gives_its_own_articles_after_a_newer_one()
    {
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());
        await bot.PutAsync(1, Documents.WithArticles(1, Documents.Article("old")));
        var before = await bot.Definitions.GetArticlesAsync(1, Token);

        await bot.PutAsync(2, Documents.WithArticles(2, Documents.Article("new")));

        Assert.Equal(before, await bot.Definitions.GetArticlesAsync(1, Token));
        Assert.Equal("old", (await bot.Definitions.GetArticlesAsync(1, Token)).Single().Id);
        Assert.Equal("new", (await bot.Definitions.GetArticlesAsync(2, Token)).Single().Id);
    }

    [Fact]
    [Trait("criterion", "AC-article-search-feed-4")]
    public async Task A_number_not_held_fails_naming_it_and_a_version_without_articles_gives_an_empty_list()
    {
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());
        await bot.PutAsync(1, Documents.Make(1));

        var failure = await Assert.ThrowsAsync<KeyNotFoundException>(() => bot.Definitions.GetArticlesAsync(7, Token));

        Assert.Contains("7", failure.Message, StringComparison.Ordinal);
        Assert.Empty(await bot.Definitions.GetArticlesAsync(1, Token));
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-article-search-feed-5")]
    [Trait("criterion", "AC-bot-runtime-sdk-17")]
    public async Task The_bots_code_hears_the_number_of_a_new_version_once_it_is_current(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        int? currentWhenTold = null;
        var articles = 0;
        var told = new TaskCompletionSource<int>();
        bot.Definitions.OnCurrentChanged(
            async (number, cancellationToken) =>
            {
                currentWhenTold = bot.Definitions.CurrentNumber;
                articles = (await bot.Definitions.GetArticlesAsync(number, cancellationToken)).Count;
                told.TrySetResult(number);
            }
        );

        await bot.PutAsync(14, Documents.WithArticles(14, Documents.Article("a")));

        Assert.Equal(14, await told.Task.WaitAsync(TimeSpan.FromSeconds(30), Token));
        Assert.Equal((14, 1), (currentWhenTold, articles));
    }

    [Fact]
    [Trait("criterion", "AC-article-search-feed-6")]
    public async Task A_repeat_and_a_refused_version_tell_nothing()
    {
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());
        var told = Listen(bot.Definitions);
        await bot.PutAsync(1, Documents.Make(1));
        await bot.PutAsync(1, Documents.Make(1));
        var bad = Documents.Make(2);
        bad["facts"] = new System.Text.Json.Nodes.JsonArray(Documents.Fact("x"), Documents.Fact("x"));
        Documents.Sign(bad);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await bot.PutAsync(2, bad)).Status);

        // Why: news runs on its own task, so a later version is what shows that nothing else was on its way.
        await bot.PutAsync(3, Documents.Make(3));
        await WaitAsync(() => told.Contains(3));
        await Task.Delay(100, Token);

        Assert.Equal([1, 3], told.Order());
    }

    [Fact]
    [Trait("criterion", "AC-article-search-feed-7")]
    public async Task A_slow_or_failing_listener_holds_up_no_answer_and_the_next_change_is_still_told()
    {
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());
        var release = new TaskCompletionSource();
        var told = new ConcurrentQueue<int>();
        bot.Definitions.OnCurrentChanged(
            async (number, _) =>
            {
                told.Enqueue(number);
                if (number == 1)
                {
                    await release.Task;
                }
                if (number == 2)
                {
                    throw new InvalidOperationException("the search is down");
                }
            }
        );

        var (first, _) = await bot.PutAsync(1, Documents.Make(1));
        var (second, _) = await bot.PutAsync(2, Documents.Make(2));
        var (third, _) = await bot.PutAsync(3, Documents.Make(3));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK), (first, second, third));
        Assert.False(release.Task.IsCompleted);
        Assert.Equal(3, (await bot.StateAsync()).Current);
        await WaitAsync(() => told.Contains(3));
        release.SetResult();
    }

    [Fact]
    [Trait("criterion", "AC-article-search-feed-7")]
    public async Task A_failing_listener_is_logged()
    {
        var logger = new RecordingLogger();
        using var runtime = new VersionRuntime("rzeczoznawca", new InMemoryVersionStore(), logger);
        runtime.OnCurrentChanged((_, _) => throw new InvalidOperationException("the search is down"));

        await runtime.PushAsync(1, Documents.Make(1).ToJsonString(), Token);

        await WaitAsync(() => logger.Errors.Any(error => error is InvalidOperationException));
    }

    [Fact]
    [Trait("criterion", "AC-article-search-feed-8")]
    public async Task An_instance_that_was_not_pushed_to_is_told_after_its_next_poll_and_gets_the_same_articles()
    {
        var shared = new InMemoryVersionStore();
        await using var a = await BotHost.StartAsync(shared);
        await using var b = await BotHost.StartAsync(shared, pollInterval: TimeSpan.FromMilliseconds(50));
        var told = Listen(b.Definitions);

        await a.PutAsync(14, Documents.WithArticles(14, Documents.Article("a")));

        await WaitAsync(() => told.Contains(14));
        Assert.Equal(await a.Definitions.GetArticlesAsync(14, Token), await b.Definitions.GetArticlesAsync(14, Token));
    }

    [Fact]
    [Trait("criterion", "AC-article-search-feed-9")]
    public async Task After_a_restart_a_listener_set_before_the_first_refresh_is_told_the_current_number_once()
    {
        var store = new InMemoryVersionStore();
        await store.WriteVersionAsync(5, Documents.Make(5).ToJsonString(), Token);
        using var runtime = new VersionRuntime("rzeczoznawca", store, new RecordingLogger());
        var told = Listen(runtime);

        await runtime.RefreshAsync(Token);
        await runtime.RefreshAsync(Token);

        await WaitAsync(() => told.Contains(5));
        await Task.Delay(100, Token);
        Assert.Equal([5], told);
    }

    [Fact]
    [Trait("criterion", "AC-article-search-feed-9")]
    public async Task A_listener_set_after_the_start_is_told_once_and_with_no_version_nothing_until_the_first_arrives()
    {
        var store = new InMemoryVersionStore();
        await using var empty = await BotHost.StartAsync(store);
        var told = Listen(empty.Definitions);
        await Task.Delay(100, Token);
        Assert.Empty(told);

        await empty.PutAsync(1, Documents.Make(1));

        await WaitAsync(() => told.Contains(1));
        var later = Listen(empty.Definitions);
        await WaitAsync(() => later.Contains(1));
        await Task.Delay(100, Token);
        Assert.Equal([1], later);
        Assert.Equal([1], told);
    }

    [Fact]
    [Trait("criterion", "AC-article-search-feed-10")]
    public async Task The_feed_reads_only_the_store_so_every_held_version_is_given_after_a_restart_with_no_cms()
    {
        using var directory = new TempDirectory();
        await using (var first = await BotHost.StartAsync(new FileVersionStore(directory.Path)))
        {
            await first.PutAsync(1, Documents.WithArticles(1, Documents.Article("one")));
            await first.PutAsync(2, Documents.WithArticles(2, Documents.Article("two")));
        }

        await using var restarted = await BotHost.StartAsync(new FileVersionStore(directory.Path));

        Assert.Equal("one", (await restarted.Definitions.GetArticlesAsync(1, Token)).Single().Id);
        Assert.Equal("two", (await restarted.Definitions.GetArticlesAsync(2, Token)).Single().Id);
    }

    [Fact]
    [Trait("criterion", "AC-article-search-feed-12")]
    public async Task Making_a_held_version_current_tells_the_number_and_repeating_it_tells_nothing()
    {
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());
        var told = Listen(bot.Definitions);
        var twelve = Documents.Make(12, "twelve");
        await bot.PutAsync(12, twelve);
        await bot.PutAsync(14, Documents.Make(14, "fourteen"));
        await WaitAsync(() => told.Contains(14));

        await bot.MakeCurrentAsync(12, twelve.Member("digest").Text());
        await bot.MakeCurrentAsync(12, twelve.Member("digest").Text());

        await WaitAsync(() => told.Count == 3);
        await Task.Delay(100, Token);
        Assert.Equal([12, 12, 14], told.Order());
    }

    private sealed class RecordingLogger : ILogger<VersionRuntime>
    {
        public ConcurrentQueue<Exception> Errors { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (logLevel == LogLevel.Error && exception is not null)
            {
                Errors.Enqueue(exception);
            }
        }
    }
}
