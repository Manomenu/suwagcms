extern alias Cli;

using System.CommandLine;
using System.Net.Http.Json;
using Suwagcms.Contracts;
using Suwagcms.Server.Tests.Articles;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Procedures;
using CliApp = Cli::Suwagcms.Cli.CliApp;
using CliEnvironment = Cli::Suwagcms.Cli.CliEnvironment;

namespace Suwagcms.Server.Tests.Versions;

// Why: a bot of the CMS registered at a given address, with a whole definition in it, so a push has something real to send.
internal sealed class PushScene(HttpClient client, string bot)
{
    private int changes;

    public string Bot { get; } = bot;

    public HttpClient Client { get; } = client;

    public FactApi Facts { get; } = new(client, bot);

    public static string FreshId() => "t" + Guid.NewGuid().ToString("N")[..12];

    public static async Task<PushScene> RegisterAsync(HttpClient client, string id, string address)
    {
        var response = await client.PostAsJsonAsync(
            "/api/bots",
            new RegisterBot(id, "A bot", address),
            TestContext.Current.CancellationToken
        );
        response.EnsureSuccessStatusCode();
        return new PushScene(client, id);
    }

    public async Task FillAsync()
    {
        Assert.True((await Facts.Add("customer.city", FactApi.Text())).IsSuccessStatusCode);
        Assert.True((await Facts.Add("order.kind", FactApi.Kind)).IsSuccessStatusCode);
        Assert.True(
            (
                await Facts.Add("system.flag", """{"type":"text","description":"Set by a procedure","maxLength":5,"writableBy":"assign"}""")
            ).IsSuccessStatusCode
        );
        var procedures = new ProcedureApi(Client, Facts);
        Assert.True((await procedures.Add("conversation", ProcedureApi.Conversation)).IsSuccessStatusCode);
        Assert.True((await procedures.Add("orderValuation", ProcedureApi.Valuation)).IsSuccessStatusCode);
        var articles = new ArticleApi(Client, Bot);
        Assert.True((await articles.KnowledgeBases.Add("offer", "Offer")).IsSuccessStatusCode);
        Assert.True((await articles.Add("cheaper", ArticleApi.Body(title: "Cheaper", content: "Cheaper options."))).IsSuccessStatusCode);
        Assert.True(
            (
                await articles.Add("priceList", ArticleApi.Body(content: "See [the cheaper ones](cms://article/cheaper)."))
            ).IsSuccessStatusCode
        );
    }

    // Why: a version is refused while nothing has changed, so each new version needs a change of its own.
    public async Task ChangeAsync() => Assert.True((await Facts.Add($"extra.n{++changes}", FactApi.Text())).IsSuccessStatusCode);

    public async Task<VersionSummary> CreateAsync()
    {
        var response = await Client.PostAsync($"/api/bots/{Bot}/versions", null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await ReadAsync<VersionCreated>(response)).Version;
    }

    public async Task<PushResult> PushAsync(int number)
    {
        var response = await Client.PostAsync($"/api/bots/{Bot}/versions/{number}/push", null, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<PushResult>(response);
    }

    public async Task<BotStatus> StatusAsync() =>
        await Client.GetFromJsonAsync<BotStatus>($"/api/bots/{Bot}/versions/status", TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("no status in the answer");

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("nothing in the answer");

    public async Task<List<PushAttempt>> Attempts() => [.. (await StatusAsync()).Attempts];

    public async Task<string> SnapshotAsync()
    {
        var parts = new List<string>();
        foreach (var path in new[] { "facts", "knowledge-bases", "articles", "procedures", "versions" })
        {
            parts.Add(await Client.GetStringAsync($"/api/bots/{Bot}/{path}", TestContext.Current.CancellationToken));
        }
        var versions =
            await Client.GetFromJsonAsync<List<VersionSummary>>($"/api/bots/{Bot}/versions", TestContext.Current.CancellationToken) ?? [];
        foreach (var version in versions)
        {
            parts.Add(await Client.GetStringAsync($"/api/bots/{Bot}/versions/{version.Number}", TestContext.Current.CancellationToken));
        }
        return string.Join("\n", parts);
    }

    public async Task<CliRun> CliAsync(params string[] words)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var environment = new CliEnvironment(
            _ => null,
            Path.Combine(Path.GetTempPath(), "suwagcms-push-tests", Guid.NewGuid().ToString("N")),
            Client
        );
        var exit = await CliApp.Run(
            [.. words, "--server", "http://localhost", "--bot", Bot],
            new InvocationConfiguration { Output = output, Error = error },
            environment
        );
        return new CliRun(exit, output.ToString(), error.ToString());
    }
}
