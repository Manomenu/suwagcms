using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
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
        // Why: seeding 5000 articles costs about 8 s, over the quick gate's budget; CI always runs this, a machine when the code of a save changes.
        var passed = Path.Combine(RepositoryRoot(), ".artifacts", $"article-scale-{InputsHash()}");
        if (Environment.GetEnvironmentVariable("CI") is null && File.Exists(passed))
        {
            return;
        }

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
        Directory.CreateDirectory(Path.GetDirectoryName(passed) ?? RepositoryRoot());
        await File.WriteAllTextAsync(passed, "", TestContext.Current.CancellationToken);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Suwagcms.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }

    private static string InputsHash()
    {
        var root = RepositoryRoot();
        var folders = new[]
        {
            "Suwagcms.Sdk/Format",
            "Suwagcms.Sdk/Validation",
            "Suwagcms.Server/Editing",
            "Suwagcms.Server/Articles",
            "Suwagcms.Server.Tests/Articles",
        };
        var files = folders
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, file)));
            hash.AppendData(File.ReadAllBytes(file));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
