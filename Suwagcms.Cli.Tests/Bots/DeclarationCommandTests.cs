using System.Net;
using System.Text.Json;

namespace Suwagcms.Cli.Tests.Bots;

public sealed class DeclarationCommandTests
{
    private static readonly string[] OnBot = ["bots", "declaration", "--server", "http://cms.internal", "--bot", "rzeczoznawca"];

    private const string Declaration =
        """{"bot":"rzeczoznawca","declaresNothing":false,"formatVersion":5,"blocks":["listen","collect","say","assign"],"codeActions":[{"id":"lookupPolicy","description":"Finds a policy","parameters":[],"results":[]}]}""";

    private static string Status(
        string ask,
        string stored,
        string declaration,
        string reason = "null",
        string at = "\"2026-11-04T21:40:00+00:00\""
    ) =>
        $$"""{"bot":"rzeczoznawca","address":"http://rzeczoznawca.internal:8080","ask":"{{ask}}","stored":"{{stored}}","declaration":{{declaration}},"answeredAt":{{at}},"reason":{{reason}},"mistakes":[],"formatFix":null,"cannotRun":[]}""";

    [Fact]
    [Trait("criterion", "AC-bot-declaration-4")]
    public async Task It_asks_the_bot_and_shows_the_format_the_blocks_and_the_actions()
    {
        var server = StubServer.Answering(HttpStatusCode.OK, Status("Answered", "Declared", Declaration));

        var result = await TestCli.Run(OnBot, server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://cms.internal/api/bots/rzeczoznawca/declaration", Assert.Single(server.Requests).ToString());
        Assert.Contains("format 5", result.Output, StringComparison.Ordinal);
        Assert.Contains("blocks listen, collect, say, assign", result.Output, StringComparison.Ordinal);
        Assert.Contains("actions lookupPolicy", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-4")]
    public async Task The_stored_option_reads_the_last_answer_without_asking_and_open_opens_the_profile()
    {
        var server = StubServer.Answering(HttpStatusCode.OK, Status("NotAsked", "Declared", Declaration));

        var stored = await TestCli.Run([.. OnBot, "--stored"], server);

        Assert.Equal(0, stored.ExitCode);
        Assert.EndsWith("/declaration?ask=false", Assert.Single(server.Requests).ToString(), StringComparison.Ordinal);
        Assert.Contains("Last answer, from 2026-11-04 21:40 UTC", stored.Output, StringComparison.Ordinal);

        Uri? opened = null;
        var open = await TestCli.Run([.. OnBot, "--open"], openBrowser: page => opened = page);
        Assert.Equal(0, open.ExitCode);
        Assert.Equal("http://cms.internal/bots/rzeczoznawca", opened?.ToString());
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-6")]
    public async Task An_unreachable_bot_shows_the_last_answer_with_its_time_and_exits_1()
    {
        var reason = "\"cannot reach http://rzeczoznawca.internal:8080\"";
        var server = StubServer.Answering(HttpStatusCode.OK, Status("Unreachable", "Declared", Declaration, reason));

        var result = await TestCli.Run(OnBot, server);

        Assert.Equal(1, result.ExitCode);
        var lines = result.Error.ReplaceLineEndings("\n").TrimEnd().Split('\n');
        Assert.Equal("Cannot reach http://rzeczoznawca.internal:8080. Last answer, from 2026-11-04 21:40 UTC:", lines[0]);
        Assert.Equal("  format 5", lines[1]);
        Assert.Equal("  blocks listen, collect, say, assign", lines[2]);
        Assert.Equal("  actions lookupPolicy", lines[3]);
        Assert.Equal("Check that the bot is running and that the CMS can reach it, then ask again.", lines[4]);
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-6")]
    public async Task With_json_a_failed_ask_returns_the_status_as_data_and_exits_1()
    {
        var reason = "\"cannot reach http://rzeczoznawca.internal:8080\"";
        var server = StubServer.Answering(HttpStatusCode.OK, Status("Unreachable", "Declared", Declaration, reason));

        var result = await TestCli.Run([.. OnBot, "--json"], server);

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.Output);
        Assert.Equal("Unreachable", document.RootElement.GetProperty("ask").GetString());
        Assert.Equal(5, document.RootElement.GetProperty("declaration").GetProperty("formatVersion").GetInt32());
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-5")]
    public async Task A_bot_that_has_never_answered_says_so()
    {
        var reason = "\"cannot reach http://rzeczoznawca.internal:8080\"";
        var server = StubServer.Answering(HttpStatusCode.OK, Status("Unreachable", "NeverAnswered", "null", reason, "null"));

        var result = await TestCli.Run(OnBot, server);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("and it has never answered", result.Error, StringComparison.Ordinal);
    }
}
