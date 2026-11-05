using System.Diagnostics;
using System.Net;
using Dapper;

namespace Suwagcms.Server.Tests.Articles;

public sealed class ArticleScaleTests(TestDatabase database) : IDisposable
{
    private const int Articles = 5000;

    // Why: about 200 ms on an idle machine and 2 s with several agents building at once, so 5 s still catches a save that grew badly.
    private const int LimitMilliseconds = 5000;

    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    // Why: every save loads and validates the whole definition, so the time a save takes grows with the articles; this keeps it from growing unnoticed.
    [Fact]
    [Trait("criterion", "AC-articles-16")]
    public async Task A_save_to_a_bot_with_5000_articles_finishes_within_the_limit()
    {
        _ = database.Pool;
        var api = await ArticleApi.NewBotAsync(app.CreateClient());
        await using (var connection = await database.Pool.OpenAsync(TestContext.Current.CancellationToken))
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO resource (id, bot_id, kind, public_id, revision, body, updated_at)
                SELECT gen_random_uuid(), @bot, 'article', 'article' || n, 1,
                       jsonb_build_object('id', 'article' || n, 'knowledgeBase', 'offer', 'title', 'Article ' || n,
                                          'content', repeat('Some Markdown text. ', 100), 'llmGuide', repeat('Talk politely. ', 30),
                                          'description', 'What article ' || n || ' answers'),
                       now()
                FROM generate_series(1, @count) AS n
                """,
                new { bot = api.Bot, count = Articles }
            );
        }
        Assert.Equal(HttpStatusCode.OK, (await api.Put("article1", ArticleApi.Body(title: "Warm up"))).StatusCode);

        // Why: load on the machine only ever adds time, so the fastest of several saves is what the code itself costs.
        var fastest = TimeSpan.MaxValue;
        for (var run = 0; run < 5; run++)
        {
            var started = Stopwatch.GetTimestamp();
            var response = await api.Put("article1", ArticleApi.Body(title: $"Run {run}"));
            var elapsed = Stopwatch.GetElapsedTime(started);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            fastest = elapsed < fastest ? elapsed : fastest;
        }

        Assert.True(
            fastest.TotalMilliseconds < LimitMilliseconds,
            $"A save to a bot with {Articles} articles took {fastest.TotalMilliseconds:F0} ms at its fastest; the limit is {LimitMilliseconds} ms."
        );
    }
}
