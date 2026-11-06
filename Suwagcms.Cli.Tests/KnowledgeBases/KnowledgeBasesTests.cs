using System.Net;
using System.Text;
using System.Text.Json;
using Suwagcms.Cli.Tests.Plugin;

namespace Suwagcms.Cli.Tests.KnowledgeBases;

public sealed class KnowledgeBasesTests
{
    private static readonly string[] OnBot = ["--server", "http://cms.internal", "--bot", "rzeczoznawca"];

    private const string Offer = """{"id":"offer","title":"Offer","language":"pl","revision":3}""";

    private const string ForValuers = """{"id":"forValuers","title":"For valuers","language":"en","revision":1}""";

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
    public async Task Kb_list_prints_every_base_with_its_id_language_and_title()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, $"[{ForValuers},{Offer}]"));

        var result = await TestCli.Run(Args("kb", "list"), server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("GET /api/bots/rzeczoznawca/knowledge-bases", $"{recorder.Calls[0].Method} {recorder.Calls[0].Url}");
        var lines = result.Output.ReplaceLineEndings("\n").TrimEnd().Split('\n');
        Assert.Equal(["forValuers  en  For valuers", "offer       pl  Offer"], lines);
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
        Assert.Equal(
            ("PUT", "/api/bots/rzeczoznawca/knowledge-bases/offer?ifRevision=0", """{"title":"Offer","language":null}"""),
            (method, url, body)
        );
        Assert.Contains("Added 'offer' (Offer, pl), revision 3.", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-1")]
    public async Task Kb_show_prints_one_base_and_json_gives_its_fields_and_revision()
    {
        var server = new Recorder().Serving(_ => (HttpStatusCode.OK, Offer));

        var text = await TestCli.Run(Args("kb", "show", "offer"), server);
        var json = await TestCli.Run([.. Args("kb", "show", "offer"), "--json"], server);

        Assert.Contains("title:    Offer", text.Output, StringComparison.Ordinal);
        Assert.Contains("language: pl", text.Output, StringComparison.Ordinal);
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
    public async Task Kb_edit_reads_the_base_then_sends_the_new_title_with_the_language_it_has_guarded_by_the_revision_it_read()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, Offer));

        var result = await TestCli.Run(Args("kb", "edit", "offer", "--title", "Our offer"), server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("GET", recorder.Calls[0].Method);
        Assert.Equal(
            ("PUT", "/api/bots/rzeczoznawca/knowledge-bases/offer?ifRevision=3", """{"title":"Our offer","language":"pl"}"""),
            (recorder.Calls[1].Method, recorder.Calls[1].Url, recorder.Calls[1].Body)
        );
        Assert.Contains("Saved 'offer'", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-2")]
    public async Task Kb_edit_of_a_base_that_does_not_exist_is_refused_and_creates_nothing()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ =>
            (
                HttpStatusCode.NotFound,
                """{"mistakes":[{"code":"resource.missing","path":"name","message":"Bot 'rzeczoznawca' has no knowledge base 'offer'.","fix":"Check the name."}]}"""
            )
        );

        var result = await TestCli.Run(Args("kb", "edit", "offer", "--title", "Our offer"), server);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(["GET"], recorder.Calls.Select(call => call.Method));
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-2")]
    public async Task Kb_edit_without_a_title_or_a_language_is_a_wrong_use_and_asks_nothing()
    {
        var server = new Recorder().Serving(_ => (HttpStatusCode.OK, Offer));

        var result = await TestCli.Run(Args("kb", "edit", "offer"), server);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(server.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-3")]
    public async Task Kb_add_sends_the_language_it_is_given_and_none_when_it_is_left_out()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, Offer));

        await TestCli.Run(Args("kb", "add", "oferta", "--title", "Oferta", "--language", "pl"), server);
        await TestCli.Run(Args("kb", "add", "offer", "--title", "Offer"), server);

        Assert.Equal("""{"title":"Oferta","language":"pl"}""", recorder.Calls[0].Body);
        Assert.Equal("""{"title":"Offer","language":null}""", recorder.Calls[1].Body);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-3")]
    public async Task Kb_edit_with_only_a_language_changes_it_and_keeps_the_title_guarded_by_the_revision_it_read()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, Offer));

        var result = await TestCli.Run(Args("kb", "edit", "offer", "--language", "en"), server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            ("PUT", "/api/bots/rzeczoznawca/knowledge-bases/offer?ifRevision=3", """{"title":"Offer","language":"en"}"""),
            (recorder.Calls[1].Method, recorder.Calls[1].Url, recorder.Calls[1].Body)
        );
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-5")]
    public async Task Kb_edit_with_only_a_title_keeps_the_language_the_base_has_and_en_does_not_overwrite_pl()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, Offer));

        await TestCli.Run(Args("kb", "edit", "offer", "--title", "Our offer"), server);

        Assert.Equal("pl", JsonDocument.Parse(recorder.Calls[1].Body).RootElement.GetProperty("language").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-4")]
    public async Task A_refused_language_prints_the_mistake_naming_both_languages_and_exits_1()
    {
        var server = StubServer.Answering(
            HttpStatusCode.UnprocessableEntity,
            """{"mistakes":[{"code":"knowledgeBase.language.unknown","path":"language","message":"Knowledge base 'offer' has language 'de'.","fix":"Use one of: pl, en."}]}"""
        );

        var result = await TestCli.Run(Args("kb", "add", "offer", "--title", "Offer", "--language", "de"), server);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("knowledgeBase.language.unknown", result.Error, StringComparison.Ordinal);
        Assert.Contains("Use one of: pl, en.", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-13")]
    public void The_main_skill_mentions_language_on_the_lines_for_adding_and_editing_bases_and_listing_articles()
    {
        var skill = SkillText.Section(
            File.ReadAllText(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", "suwagcms", "SKILL.md")),
            "Commands"
        );

        foreach (var command in new[] { "kb add", "kb edit", "articles list" })
        {
            var line = skill
                .ReplaceLineEndings("\n")
                .Split('\n')
                .Single(text => text.StartsWith($"- `suwagcms {command} ", StringComparison.Ordinal));
            Assert.Contains("--language", line, StringComparison.Ordinal);
        }
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
    public async Task If_revision_is_sent_as_given_on_an_edit_and_on_a_delete_and_a_stale_refusal_prints_the_current_revision()
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

        var edit = await TestCli.Run(Args("kb", "edit", "offer", "--title", "x", "--if-revision", "1"), server);
        var delete = await TestCli.Run(Args("kb", "delete", "offer", "--if-revision", "1"), server);

        Assert.Equal([1, 1], [edit.ExitCode, delete.ExitCode]);
        Assert.EndsWith("?ifRevision=1", recorder.Calls[1].Url, StringComparison.Ordinal);
        Assert.EndsWith("?ifRevision=1", recorder.Calls[2].Url, StringComparison.Ordinal);
        Assert.Contains("revision 3", edit.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-6")]
    public async Task Dry_run_on_an_add_an_edit_and_a_delete_asks_for_a_dry_run_and_says_nothing_was_changed()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(request =>
            request.Method == HttpMethod.Delete ? (HttpStatusCode.NoContent, "") : (HttpStatusCode.OK, Offer)
        );

        var add = await TestCli.Run(Args("kb", "add", "offer", "--title", "Offer", "--dry-run"), server);
        var edit = await TestCli.Run(Args("kb", "edit", "offer", "--title", "Our offer", "--dry-run"), server);
        var delete = await TestCli.Run(Args("kb", "delete", "offer", "--dry-run"), server);

        Assert.Equal("/api/bots/rzeczoznawca/knowledge-bases/offer?ifRevision=0&dryRun=true", recorder.Calls[0].Url);
        Assert.EndsWith("?ifRevision=3&dryRun=true", recorder.Calls[2].Url, StringComparison.Ordinal);
        Assert.EndsWith("?dryRun=true", recorder.Calls[3].Url, StringComparison.Ordinal);
        Assert.All([add, edit, delete], result => Assert.Contains("Nothing was changed.", result.Output, StringComparison.Ordinal));
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
    [Trait("criterion", "AC-cli-foundation-9")]
    public async Task Open_shows_the_knowledge_bases_screen_of_the_bot_without_calling_the_server()
    {
        var opened = new List<Uri>();
        var server = StubServer.Healthy();

        var result = await TestCli.Run(Args("kb", "list", "--open"), server, openBrowser: opened.Add);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://cms.internal/bots/rzeczoznawca/knowledge-bases", Assert.Single(opened).ToString());
        Assert.Empty(server.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-22")]
    public async Task A_delete_of_a_base_has_no_page_to_open()
    {
        var result = await TestCli.Run(Args("kb", "delete", "--title", "--open"), StubServer.Healthy());

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("what it deletes is gone", result.Error, StringComparison.Ordinal);
    }
}
