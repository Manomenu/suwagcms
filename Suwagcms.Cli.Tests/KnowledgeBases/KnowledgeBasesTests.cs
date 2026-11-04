using System.Net;
using System.Text;
using System.Text.Json;
using Suwagcms.Cli.Tests.Plugin;

namespace Suwagcms.Cli.Tests.KnowledgeBases;

public sealed class KnowledgeBasesTests
{
    private static readonly string[] OnBot = ["--server", "http://cms.internal", "--bot", "rzeczoznawca"];

    private const string Offer = """{"id":"offer","title":"Offer","revision":3}""";

    private const string ForValuers = """{"id":"forValuers","title":"For valuers","revision":1}""";

    private sealed class Recorder
    {
        public List<(string Method, string Url, string Body)> Calls { get; } = [];

        public StubServer Serving(Func<HttpRequestMessage, (HttpStatusCode, string)> answer) =>
            new(request =>
            {
                using var reader = new StreamReader(request.Content?.ReadAsStream() ?? Stream.Null);
                Calls.Add((request.Method.Method, request.RequestUri?.PathAndQuery ?? "", reader.ReadToEnd()));
                var (status, json) = answer(request);
                return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            });
    }

    private static string[] Args(params string[] words) => [.. words, .. OnBot];

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-1")]
    public async Task Kb_list_prints_every_base_with_its_id_and_title()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, $"[{ForValuers},{Offer}]"));

        var result = await TestCli.Run(Args("kb", "list"), server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("GET /api/bots/rzeczoznawca/knowledge-bases", $"{recorder.Calls[0].Method} {recorder.Calls[0].Url}");
        var lines = result.Output.ReplaceLineEndings("\n").TrimEnd().Split('\n');
        Assert.Equal(["forValuers  For valuers", "offer       Offer"], lines);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-1")]
    public async Task Kb_add_sends_the_title_and_requires_that_the_id_is_free()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, Offer));

        var result = await TestCli.Run(Args("kb", "add", "offer", "--title", "Offer"), server);

        Assert.Equal(0, result.ExitCode);
        var (method, url, body) = Assert.Single(recorder.Calls);
        Assert.Equal(("PUT", "/api/bots/rzeczoznawca/knowledge-bases/offer?ifRevision=0", """{"title":"Offer"}"""), (method, url, body));
        Assert.Contains("Added 'offer' (Offer), revision 3.", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-1")]
    public async Task Kb_show_prints_one_base_and_json_gives_its_fields_and_revision()
    {
        var server = new Recorder().Serving(_ => (HttpStatusCode.OK, Offer));

        var text = await TestCli.Run(Args("kb", "show", "offer"), server);
        var json = await TestCli.Run([.. Args("kb", "show", "offer"), "--json"], server);

        Assert.Contains("title:    Offer", text.Output, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json.Output);
        Assert.Equal(
            ("offer", "Offer", 3),
            (
                document.RootElement.GetProperty("id").GetString(),
                document.RootElement.GetProperty("title").GetString(),
                document.RootElement.GetProperty("revision").GetInt32()
            )
        );
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-2")]
    public async Task Kb_rename_reads_the_base_then_sends_the_new_title_guarded_by_the_revision_it_read()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, Offer));

        var result = await TestCli.Run(Args("kb", "rename", "offer", "--title", "Our offer"), server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("GET", recorder.Calls[0].Method);
        Assert.Equal(
            ("PUT", "/api/bots/rzeczoznawca/knowledge-bases/offer?ifRevision=3", """{"title":"Our offer"}"""),
            (recorder.Calls[1].Method, recorder.Calls[1].Url, recorder.Calls[1].Body)
        );
        Assert.Contains("Renamed 'offer'", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-2")]
    public async Task Kb_rename_of_a_base_that_does_not_exist_is_refused_and_creates_nothing()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ =>
            (
                HttpStatusCode.NotFound,
                """{"mistakes":[{"code":"resource.missing","path":"name","message":"Bot 'rzeczoznawca' has no knowledge base 'offer'.","fix":"Check the name."}]}"""
            )
        );

        var result = await TestCli.Run(Args("kb", "rename", "offer", "--title", "Our offer"), server);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(["GET"], recorder.Calls.Select(call => call.Method));
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-2")]
    public async Task Kb_rename_without_a_title_is_a_wrong_use_and_asks_nothing()
    {
        var server = new Recorder().Serving(_ => (HttpStatusCode.OK, Offer));

        var result = await TestCli.Run(Args("kb", "rename", "offer"), server);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(server.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-3")]
    public async Task Kb_delete_deletes_the_named_base_and_says_so()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.NoContent, ""));

        var result = await TestCli.Run(Args("kb", "delete", "offer"), server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(("DELETE", "/api/bots/rzeczoznawca/knowledge-bases/offer"), (recorder.Calls[0].Method, recorder.Calls[0].Url));
        Assert.Contains("Deleted 'offer'", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-4")]
    public async Task A_refused_add_prints_every_mistake_with_its_code_message_and_fix_and_exits_1()
    {
        var server = StubServer.Answering(
            HttpStatusCode.UnprocessableEntity,
            """
            {"mistakes":[
              {"code":"knowledgeBase.id.invalid","path":"id","message":"'Offer' is not a knowledge base id.","fix":"Rename the knowledge base to 'offer'."},
              {"code":"knowledgeBase.title.missing","path":"title","message":"Knowledge base 'Offer' has no title.","fix":"Add one."}]}
            """
        );

        var result = await TestCli.Run(Args("kb", "add", "Offer"), server);

        Assert.Equal(1, result.ExitCode);
        foreach (
            var expected in new[]
            {
                "knowledgeBase.id.invalid",
                "Rename the knowledge base to 'offer'.",
                "knowledgeBase.title.missing",
                "Add one.",
            }
        )
        {
            Assert.Contains(expected, result.Error, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-6")]
    public async Task If_revision_is_sent_as_given_on_a_rename_and_on_a_delete_and_a_stale_refusal_prints_the_current_revision()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(request =>
            request.Method == HttpMethod.Get
                ? (HttpStatusCode.OK, Offer)
                : (
                    HttpStatusCode.Conflict,
                    """{"mistakes":[{"code":"resource.stale","path":"revision","message":"changed: it is now revision 3.","fix":"Read it again (revision 3)."}]}"""
                )
        );

        var rename = await TestCli.Run(Args("kb", "rename", "offer", "--title", "x", "--if-revision", "1"), server);
        var delete = await TestCli.Run(Args("kb", "delete", "offer", "--if-revision", "1"), server);

        Assert.Equal([1, 1], [rename.ExitCode, delete.ExitCode]);
        Assert.EndsWith("?ifRevision=1", recorder.Calls[1].Url, StringComparison.Ordinal);
        Assert.EndsWith("?ifRevision=1", recorder.Calls[2].Url, StringComparison.Ordinal);
        Assert.Contains("revision 3", rename.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-6")]
    public async Task Dry_run_on_an_add_a_rename_and_a_delete_asks_for_a_dry_run_and_says_nothing_was_changed()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(request =>
            request.Method == HttpMethod.Delete ? (HttpStatusCode.NoContent, "") : (HttpStatusCode.OK, Offer)
        );

        var add = await TestCli.Run(Args("kb", "add", "offer", "--title", "Offer", "--dry-run"), server);
        var rename = await TestCli.Run(Args("kb", "rename", "offer", "--title", "Our offer", "--dry-run"), server);
        var delete = await TestCli.Run(Args("kb", "delete", "offer", "--dry-run"), server);

        Assert.Equal("/api/bots/rzeczoznawca/knowledge-bases/offer?ifRevision=0&dryRun=true", recorder.Calls[0].Url);
        Assert.EndsWith("?ifRevision=3&dryRun=true", recorder.Calls[2].Url, StringComparison.Ordinal);
        Assert.EndsWith("?dryRun=true", recorder.Calls[3].Url, StringComparison.Ordinal);
        Assert.All([add, rename, delete], result => Assert.Contains("Nothing was changed.", result.Output, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-7")]
    public async Task A_bot_with_no_bases_prints_nothing_and_exits_0_and_json_gives_an_empty_list()
    {
        var server = new Recorder().Serving(_ => (HttpStatusCode.OK, "[]"));

        var text = await TestCli.Run(Args("kb", "list"), server);
        var json = await TestCli.Run([.. Args("kb", "list"), "--json"], server);

        Assert.Equal((0, ""), (text.ExitCode, text.Output));
        Assert.Equal(0, json.ExitCode);
        using var document = JsonDocument.Parse(json.Output);
        Assert.Equal(0, document.RootElement.GetProperty("knowledgeBases").GetArrayLength());
    }

    [Fact]
    public async Task Open_shows_the_knowledge_bases_screen_of_the_bot_without_calling_the_server()
    {
        var opened = new List<Uri>();
        var server = StubServer.Healthy();

        var result = await TestCli.Run(Args("kb", "list", "--open"), server, openBrowser: opened.Add);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://cms.internal/bots/rzeczoznawca/knowledge-bases", Assert.Single(opened).ToString());
        Assert.Empty(server.Requests);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("rename")]
    [InlineData("delete")]
    public async Task A_command_that_changes_a_base_has_no_page_to_open(string word)
    {
        var result = await TestCli.Run(Args("kb", word, "offer", "--title", "x", "--open"), StubServer.Healthy());

        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-8")]
    public void Every_kb_command_has_its_line_in_the_main_skill()
    {
        var skill = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", "suwagcms", "SKILL.md"));
        var commands = CommandList.RunnableInCli().Where(words => words.StartsWith("kb ", StringComparison.Ordinal)).ToList();

        Assert.Equal(["kb add", "kb delete", "kb list", "kb rename", "kb show"], commands);
        Assert.Empty(commands.Except(CommandList.Listed(skill)));
    }
}
