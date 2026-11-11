extern alias Cli;

using System.CommandLine;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using Suwagcms.Server.Database;
using Suwagcms.Server.Tests.Articles;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Personas;
using CliApp = Cli::Suwagcms.Cli.CliApp;
using CliEnvironment = Cli::Suwagcms.Cli.CliEnvironment;

namespace Suwagcms.Server.Tests.Changes;

// Why: one fresh bot and the two ways a file reaches it, the request and the real command, so a test can compare either with what a resource then reads back as.
internal sealed class ChangeScene(HttpClient client, string bot)
{
    public string Bot { get; } = bot;

    public FactApi Facts { get; } = new(client, bot);

    public ArticleApi Articles { get; } = new(client, bot);

    public PersonaApi Persona { get; } = new(client, bot);

    public static async Task<ChangeScene> NewAsync(HttpClient client) => new(client, (await FactApi.NewBotAsync(client)).Bot);

    public async Task<HttpResponseMessage> Post(string json, bool dryRun = false, bool replace = false)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        string[] query = [.. dryRun ? ["dryRun=true"] : Array.Empty<string>(), .. replace ? ["replace=true"] : Array.Empty<string>()];
        return await client.PostAsync(
            $"/api/bots/{Bot}/changes{(query.Length == 0 ? "" : "?" + string.Join('&', query))}",
            content,
            TestContext.Current.CancellationToken
        );
    }

    public static string File(params string[] changes) => $$"""{"changes":[{{string.Join(',', changes)}}]}""";

    public static string AddFact(string name, string description = "A fact") =>
        $$"""{"add":"fact","name":"{{name}}","type":"text","description":"{{description}}","maxLength":80}""";

    public static string AddBase(string id, string title = "A base") =>
        $$"""{"add":"knowledgeBase","id":"{{id}}","title":"{{title}}","embedding":"default","results":5}""";

    public static string AddArticle(string id, string knowledgeBase, string content = "What is true.") =>
        $$"""{"add":"article","id":"{{id}}","knowledgeBase":"{{knowledgeBase}}","title":"Title of {{id}}","content":"{{content}}","llmGuide":"Ask first.","description":"About {{id}}"}""";

    public static async Task<JsonElement> JsonOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    public async Task<CliRun> CliAsync(string fileText, params string[] words)
    {
        var folder = Path.Combine(Path.GetTempPath(), "suwagcms-apply-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "changes.yaml");
        await System.IO.File.WriteAllTextAsync(path, fileText, TestContext.Current.CancellationToken);
        return await CommandIn(folder, ["apply", path, .. words]);
    }

    public async Task<CliRun> CommandAsync(params string[] words)
    {
        var folder = Path.Combine(Path.GetTempPath(), "suwagcms-apply-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return await CommandIn(folder, words);
    }

    private async Task<CliRun> CommandIn(string folder, string[] words)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = await CliApp.Run(
            [.. words, "--server", "http://localhost", "--bot", Bot],
            new InvocationConfiguration { Output = output, Error = error },
            new CliEnvironment(_ => null, folder, client)
        );
        return new CliRun(exit, output.ToString(), error.ToString());
    }

    public async Task<string> ReadAsync(string collection, string id)
    {
        var response = await client.GetAsync(
            $"/api/bots/{Bot}/{collection}/{Uri.EscapeDataString(id)}",
            TestContext.Current.CancellationToken
        );
        return response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
            : "absent";
    }

    // Why: every row as stored, so "nothing changed" also means its revision and its time did not move.
    public async Task<List<string>> RowsAsync(Pool pool)
    {
        await using var connection = await pool.OpenAsync(TestContext.Current.CancellationToken);
        var rows = await connection.QueryAsync<string>(
            """
            SELECT kind || '/' || public_id || '/' || revision || '/' || updated_at::text || '/' || body::text
            FROM resource WHERE bot_id = @bot ORDER BY kind, public_id
            """,
            new { bot = Bot }
        );
        return [.. rows];
    }

    public async Task<int> HistoryCountAsync(Pool pool)
    {
        await using var connection = await pool.OpenAsync(TestContext.Current.CancellationToken);
        return await connection.ExecuteScalarAsync<int>("SELECT count(*) FROM change_entry WHERE bot_id = @bot", new { bot = Bot });
    }
}
