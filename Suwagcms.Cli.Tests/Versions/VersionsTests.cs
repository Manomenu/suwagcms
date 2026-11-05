using System.Net;
using System.Text;
using System.Text.Json;
using Suwagcms.Cli.Tests.Plugin;

namespace Suwagcms.Cli.Tests.Versions;

public sealed class VersionsTests
{
    private static readonly string[] OnBot = ["--server", "http://cms.internal", "--bot", "rzeczoznawca"];

    private const string V14 =
        """{"number":14,"createdAt":"2026-11-04T21:40:00+00:00","facts":3,"knowledgeBases":1,"articles":12,"procedures":2,"sha256":"3f9a0000000000000000000000000000000000000000000000000000000fc21e"}""";

    private const string V13 =
        """{"number":13,"createdAt":"2026-11-02T09:12:00+00:00","facts":3,"knowledgeBases":1,"articles":11,"procedures":2,"sha256":"b07d000000000000000000000000000000000000000000000000000000001a44"}""";

    private sealed class Recorder
    {
        public List<(string Method, string Url)> Calls { get; } = [];

        public StubServer Serving(HttpStatusCode status, string json) =>
            new(request =>
            {
                Calls.Add((request.Method.Method, request.RequestUri?.PathAndQuery ?? ""));
                return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            });
    }

    private static string[] Args(params string[] words) => [.. words, .. OnBot];

    [Fact]
    [Trait("criterion", "AC-version-create-13")]
    public async Task Versions_list_prints_each_version_with_number_date_counts_and_digest_newest_first()
    {
        var recorder = new Recorder();

        var result = await TestCli.Run(Args("versions", "list"), recorder.Serving(HttpStatusCode.OK, $"[{V14},{V13}]"));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(("GET", "/api/bots/rzeczoznawca/versions"), Assert.Single(recorder.Calls));
        var lines = result.Output.ReplaceLineEndings("\n").TrimEnd().Split('\n');
        Assert.Equal("v14  2026-11-04 21:40  3 facts, 1 knowledge bases, 12 articles, 2 procedures  3f9a…c21e", lines[0]);
        Assert.StartsWith(
            "v13  2026-11-02 09:12  3 facts, 1 knowledge bases, 11 articles, 2 procedures",
            lines[1],
            StringComparison.Ordinal
        );
    }

    [Fact]
    [Trait("criterion", "AC-version-create-13")]
    public async Task Versions_list_of_a_bot_without_versions_offers_to_create_one()
    {
        var result = await TestCli.Run(Args("versions", "list"), new Recorder().Serving(HttpStatusCode.OK, "[]"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("suwagcms versions create", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-create-13")]
    public async Task Versions_show_prints_the_version_with_its_document_exactly_as_stored_and_json_gives_both()
    {
        var detail = $$"""{"version":{{V14}},"document":"{\"formatVersion\":1,\"number\":14}"}""";
        var recorder = new Recorder();
        var server = recorder.Serving(HttpStatusCode.OK, detail);

        var text = await TestCli.Run(Args("versions", "show", "14"), server);
        var json = await TestCli.Run([.. Args("versions", "show", "14"), "--json"], server);

        Assert.Equal(("GET", "/api/bots/rzeczoznawca/versions/14"), recorder.Calls[0]);
        Assert.Contains("sha256 3f9a", text.Output, StringComparison.Ordinal);
        Assert.Contains("""{"formatVersion":1,"number":14}""", text.Output, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json.Output);
        Assert.Equal(14, document.RootElement.GetProperty("version").GetProperty("number").GetInt32());
        Assert.Equal("""{"formatVersion":1,"number":14}""", document.RootElement.GetProperty("document").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-version-create-1")]
    public async Task Versions_create_posts_and_says_which_version_was_created()
    {
        var recorder = new Recorder();

        var result = await TestCli.Run(
            Args("versions", "create"),
            recorder.Serving(HttpStatusCode.Created, $$"""{"version":{{V14}},"written":true}""")
        );

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(("POST", "/api/bots/rzeczoznawca/versions"), Assert.Single(recorder.Calls));
        Assert.Equal("Created v14  sha256 3f9a…c21e", result.Output.Trim());
    }

    [Fact]
    [Trait("criterion", "AC-version-create-2")]
    public async Task A_refused_creation_prints_every_mistake_with_its_fix_and_exits_1()
    {
        var server = StubServer.Answering(
            HttpStatusCode.UnprocessableEntity,
            """
            {"title":"The version was not created.","status":422,"detail":"2 mistake(s)","mistakes":[
              {"code":"bot.conversation.missing","stage":"Version","resource":{"kind":"Definition","id":""},"path":"procedures","message":"This bot has no 'conversation' procedure.","fix":"Add it.","related":[]},
              {"code":"step.unreachable","stage":"Version","resource":{"kind":"Procedure","id":"orderValuation"},"path":"steps.goodbye","message":"No path reaches 'goodbye'.","fix":"Connect it or delete it.","related":[]}]}
            """
        );

        var result = await TestCli.Run(Args("versions", "create"), server);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Not created, 2 mistake(s):", result.Error, StringComparison.Ordinal);
        Assert.Contains("bot.conversation.missing", result.Error, StringComparison.Ordinal);
        Assert.Contains("step.unreachable", result.Error, StringComparison.Ordinal);
        Assert.Contains("Fix: Connect it or delete it.", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-create-12")]
    public async Task Dry_run_posts_with_dryRun_and_says_a_version_could_be_created()
    {
        var recorder = new Recorder();

        var result = await TestCli.Run(
            Args("versions", "create", "--dry-run"),
            recorder.Serving(HttpStatusCode.OK, $$"""{"version":{{V14}},"written":false}""")
        );

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(("POST", "/api/bots/rzeczoznawca/versions?dryRun=true"), Assert.Single(recorder.Calls));
        Assert.Contains("A version could be created: v14", result.Output, StringComparison.Ordinal);
        Assert.Contains("Nothing was changed", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-create-15")]
    public void Each_versions_command_has_its_line_in_the_main_skill()
    {
        var skill = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", "suwagcms", "SKILL.md"));

        var listed = CommandList.Listed(skill);

        Assert.Contains("versions list", listed);
        Assert.Contains("versions create", listed);
        Assert.Contains("versions show", listed);
    }
}
