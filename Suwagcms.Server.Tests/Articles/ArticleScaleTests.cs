using System.Diagnostics;
using System.Net;
using Dapper;

namespace Suwagcms.Server.Tests.Articles;

public sealed class ArticleScaleTests(TestDatabase database) : IDisposable
{
    private const int Articles = 5000;

    // Why: the first measurement was 204 ms for the slowest of three saves, so the limit is 1000 ms: about five times that, enough for a loaded machine and too little for a rule that is slow per pair of articles.
    private const int LimitMilliseconds = 1000;

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

        var slowest = TimeSpan.Zero;
        for (var run = 0; run < 3; run++)
        {
            var started = Stopwatch.GetTimestamp();
            var response = await api.Put("article1", ArticleApi.Body(title: $"Run {run}"));
            var elapsed = Stopwatch.GetElapsedTime(started);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            slowest = elapsed > slowest ? elapsed : slowest;
        }

        Assert.True(
            slowest.TotalMilliseconds < LimitMilliseconds,
            $"A save to a bot with {Articles} articles took {slowest.TotalMilliseconds:F0} ms; the limit is {LimitMilliseconds} ms."
        );
    }
}
