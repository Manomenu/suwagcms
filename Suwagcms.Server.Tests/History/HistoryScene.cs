extern alias Cli;

using System.CommandLine;
using System.Net;
using System.Net.Http.Json;
using Suwagcms.Contracts.History;
using CliApp = Cli::Suwagcms.Cli.CliApp;
using CliEnvironment = Cli::Suwagcms.Cli.CliEnvironment;

namespace Suwagcms.Server.Tests.History;

internal sealed class HistoryScene(HttpClient client, string bot)
{
    public string Bot { get; } = bot;

    public async Task<HistoryPage> OfBot(string query = "") => await Read($"/api/bots/{Bot}/history{query}");

    public async Task<HistoryPage> Of(string collection, string id, string query = "") =>
        await Read($"/api/bots/{Bot}/{collection}/{Uri.EscapeDataString(id)}/history{query}");

    private static readonly System.Text.Json.JsonSerializerOptions Web = new(System.Text.Json.JsonSerializerDefaults.Web);

    private async Task<HistoryPage> Read(string path)
    {
        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<HistoryPage>(Web, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("no page in the answer");
    }

    public static string TextOf(FieldChange change, bool old) => (old ? change.Old : change.New)?.GetString() ?? "";

    public async Task<CliRun> CliAsync(params string[] words)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = await CliApp.Run(
            [.. words, "--server", "http://localhost", "--bot", Bot],
            new InvocationConfiguration { Output = output, Error = error },
            new CliEnvironment(_ => null, Path.Combine(Path.GetTempPath(), "suwagcms-history-tests", Guid.NewGuid().ToString("N")), client)
        );
        return new CliRun(exit, output.ToString(), error.ToString());
    }
}
