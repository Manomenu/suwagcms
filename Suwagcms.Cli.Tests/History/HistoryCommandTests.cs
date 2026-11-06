using System.Net;
using System.Text.Json.Nodes;

namespace Suwagcms.Cli.Tests.History;

public sealed class HistoryCommandTests
{
    private static readonly string[] OnBot = ["--server", "http://cms.internal", "--bot", "rzeczoznawca"];

    private const string Edited = """
        {"entries":[{"id":9,"at":"2026-11-04T21:40:00+00:00","kind":"article","resourceId":"cennik","action":"edited","revision":8,
        "changes":[{"field":"content","old":"Cennik\nWycena domu: 900 zł\nKoniec","new":"Cennik\nWycena domu: 1100 zł\nKoniec"}],"batch":null},
        {"id":3,"at":"2026-11-02T09:12:00+00:00","kind":"article","resourceId":"cennik","action":"added","revision":1,
        "changes":[{"field":"title","old":null,"new":"Cennik"}],"batch":"3f2a1b9c-0000-0000-0000-000000000000"}],
        "next":3,"since":"2026-11-01T08:00:00+00:00"}
        """;

    [Fact]
    [Trait("criterion", "AC-change-history-1")]
    public async Task The_form_for_one_resource_prints_what_the_spec_shows_only_the_lines_that_differ()
    {
        var server = StubServer.Answering(HttpStatusCode.OK, Edited);

        var result = await TestCli.Run(["history", "article", "cennik", .. OnBot], server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://cms.internal/api/bots/rzeczoznawca/articles/cennik/history", server.Requests[0].AbsoluteUri);
        var lines = result.Output.ReplaceLineEndings("\n").TrimEnd().Split('\n');
        Assert.StartsWith("2026-11-04 21:40  edited", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("revision 8", lines[0], StringComparison.Ordinal);
        Assert.Equal(["  content", "    - Wycena domu: 900 zł", "    + Wycena domu: 1100 zł"], lines[1..4]);
        Assert.Contains("added", lines[4], StringComparison.Ordinal);
        Assert.Contains("batch 3f2a1b9c", lines[4], StringComparison.Ordinal);
        Assert.Equal("More: --before 3", lines[5]);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-6")]
    public async Task The_form_for_the_bot_names_each_resource_and_sends_the_page_it_is_asked_for()
    {
        var server = StubServer.Answering(HttpStatusCode.OK, Edited);

        var result = await TestCli.Run(["history", "--limit", "2", "--before", "40", .. OnBot], server);

        Assert.Equal("http://cms.internal/api/bots/rzeczoznawca/history?limit=2&before=40", server.Requests[0].AbsoluteUri);
        Assert.Contains("article cennik", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-6")]
    public async Task A_kind_without_an_id_or_a_limit_below_one_is_wrong_use()
    {
        Assert.Equal(2, (await TestCli.Run(["history", "article", .. OnBot], StubServer.Healthy())).ExitCode);
        Assert.Equal(2, (await TestCli.Run(["history", "--limit", "0", .. OnBot], StubServer.Healthy())).ExitCode);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-9")]
    public async Task Json_for_the_whole_bot_returns_the_same_entries()
    {
        var server = StubServer.Answering(HttpStatusCode.OK, Edited);

        var result = await TestCli.Run(["history", "--json", .. OnBot], server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://cms.internal/api/bots/rzeczoznawca/history", server.Requests[0].AbsoluteUri);
        var entries = JsonNode.Parse(result.Output)?["entries"]?.AsArray();
        Assert.Equal(2, entries?.Count);
        Assert.Equal(9, entries?[0]?["id"]?.GetValue<int>());
        Assert.Equal("cennik", entries?[0]?["resourceId"]?.GetValue<string>());
        Assert.Equal("added", entries?[1]?["action"]?.GetValue<string>());
    }

    [Fact]
    [Trait("criterion", "AC-change-history-9")]
    public async Task Open_opens_the_matching_page_for_the_bot_and_for_one_resource()
    {
        var opened = new List<Uri>();

        await TestCli.Run(["history", "--open", .. OnBot], StubServer.Healthy(), openBrowser: opened.Add);
        await TestCli.Run(["history", "kb", "oferta", "--open", .. OnBot], StubServer.Healthy(), openBrowser: opened.Add);

        Assert.Equal("http://cms.internal/bots/rzeczoznawca/history", opened[0].AbsoluteUri);
        Assert.Equal("http://cms.internal/bots/rzeczoznawca/history?kind=knowledge-bases&id=oferta", opened[1].AbsoluteUri);
    }

    private const string IncompleteWindow = """
        {"entries":[],"next":null,"since":"2026-11-04T08:00:00+00:00","incomplete":true}
        """;

    [Fact]
    [Trait("criterion", "AC-change-history-33")]
    public async Task The_window_of_a_version_asks_its_route_and_names_each_resource()
    {
        var server = StubServer.Answering(HttpStatusCode.OK, Edited);

        var result = await TestCli.Run(["versions", "window", "14", "--limit", "2", "--before", "40", .. OnBot], server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://cms.internal/api/bots/rzeczoznawca/versions/14/window?limit=2&before=40", server.Requests[0].AbsoluteUri);
        Assert.Contains("article cennik", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("may be incomplete", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-33")]
    public async Task A_window_that_opens_before_history_says_the_list_may_be_incomplete()
    {
        var result = await TestCli.Run(["versions", "window", "3", .. OnBot], StubServer.Answering(HttpStatusCode.OK, IncompleteWindow));

        Assert.Contains(
            "Changes made before 2026-11-04 were not recorded, so this list may be incomplete.",
            result.Output,
            StringComparison.Ordinal
        );
        Assert.Contains("No change has been recorded for this version.", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-33")]
    public async Task Open_opens_the_versions_page_and_json_gives_the_incomplete_flag()
    {
        var opened = new List<Uri>();

        await TestCli.Run(["versions", "window", "14", "--open", .. OnBot], StubServer.Healthy(), openBrowser: opened.Add);
        var json = await TestCli.Run(
            ["versions", "window", "3", "--json", .. OnBot],
            StubServer.Answering(HttpStatusCode.OK, IncompleteWindow)
        );

        Assert.Equal("http://cms.internal/bots/rzeczoznawca/versions/14", opened[0].AbsoluteUri);
        Assert.True(JsonNode.Parse(json.Output)?["incomplete"]?.GetValue<bool>());
    }

    [Fact]
    [Trait("criterion", "AC-change-history-23")]
    public async Task What_history_prints_as_text_stays_with_its_own_words_and_marks()
    {
        var result = await TestCli.Run(["history", "article", "cennik", .. OnBot], StubServer.Answering(HttpStatusCode.OK, Edited));

        Assert.Contains("    - Wycena domu: 900 zł", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("→", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("  Cennik", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-change-history-33")]
    public void The_plugins_skill_says_what_the_window_command_does()
    {
        var skill = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", "suwagcms", "SKILL.md"));

        Assert.Contains("suwagcms versions window <number>", skill, StringComparison.Ordinal);
    }
}
