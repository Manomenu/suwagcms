using System.Net;
using System.Text;
using System.Text.Json;
using Suwagcms.Cli.Tests.Plugin;

namespace Suwagcms.Cli.Tests.Articles;

public sealed class ArticlesTests
{
    private static readonly string[] OnBot = ["--server", "http://cms.internal", "--bot", "rzeczoznawca"];

    private const string PriceList =
        """{"id":"priceList","knowledgeBase":"offer","title":"Price list","content":"# Price\n\nFrom 400 zl.\n","llmGuide":"Ask the kind first.","description":"What a valuation costs","revision":3}""";

    private const string Guide =
        """{"id":"guide","knowledgeBase":"forValuers","title":"Guide","content":"c","llmGuide":"g","description":"For valuers","revision":1}""";

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

    private static string Scratch(string name, byte[] bytes)
    {
        var folder = Path.Combine(Path.GetTempPath(), "suwagcms-cli-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static JsonElement BodyOf((string Method, string Url, string Body) call) => JsonDocument.Parse(call.Body).RootElement.Clone();

    [Fact]
    [Trait("criterion", "AC-articles-1")]
    public async Task Articles_list_prints_every_article_with_its_id_title_and_base()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, $"[{PriceList},{Guide}]"));

        var result = await TestCli.Run(Args("articles", "list"), server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("GET /api/bots/rzeczoznawca/articles", $"{recorder.Calls[0].Method} {recorder.Calls[0].Url}");
        Assert.Equal(
            ["priceList  Price list  offer", "guide      Guide       forValuers"],
            result.Output.ReplaceLineEndings("\n").TrimEnd().Split('\n')
        );
    }

    [Fact]
    [Trait("criterion", "AC-articles-1")]
    public async Task Articles_add_sends_every_field_as_given_and_the_id_must_be_free()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, PriceList));

        var result = await TestCli.Run(
            Args(
                "articles",
                "add",
                "priceList",
                "--kb",
                "offer",
                "--title",
                "Price list",
                "--content",
                "From 400 zl.",
                "--guide",
                "Ask the kind first.",
                "--description",
                "What a valuation costs"
            ),
            server
        );

        Assert.Equal(0, result.ExitCode);
        var (method, url, body) = Assert.Single(recorder.Calls);
        Assert.Equal(("PUT", "/api/bots/rzeczoznawca/articles/priceList?ifRevision=0"), (method, url));
        Assert.Equal(
            """{"knowledgeBase":"offer","title":"Price list","content":"From 400 zl.","llmGuide":"Ask the kind first.","description":"What a valuation costs"}""",
            body
        );
        Assert.Contains("Added 'priceList' (Price list, in offer), revision 3.", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-articles-1")]
    public async Task Articles_show_prints_one_article_in_full_and_json_gives_its_fields_and_revision()
    {
        var server = new Recorder().Serving(_ => (HttpStatusCode.OK, PriceList));

        var text = await TestCli.Run(Args("articles", "show", "priceList"), server);
        var json = await TestCli.Run([.. Args("articles", "show", "priceList"), "--json"], server);

        Assert.Contains("knowledge base: offer", text.Output, StringComparison.Ordinal);
        Assert.Contains("From 400 zl.", text.Output, StringComparison.Ordinal);
        Assert.Contains("Ask the kind first.", text.Output, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json.Output);
        Assert.Equal("# Price\n\nFrom 400 zl.\n", document.RootElement.GetProperty("content").GetString());
        Assert.Equal(3, document.RootElement.GetProperty("revision").GetInt32());
    }

    [Fact]
    [Trait("criterion", "AC-articles-3")]
    public async Task A_refused_add_prints_every_mistake_with_its_code_message_and_fix_and_exits_1()
    {
        var server = StubServer.Answering(
            HttpStatusCode.UnprocessableEntity,
            """
            {"mistakes":[
              {"code":"article.llmGuide.missing","path":"llmGuide","message":"'cennik' has no llmGuide.","fix":"Say how the model should talk about it: tone, what to ask first, what not to say."},
              {"code":"article.knowledgeBase.unknown","path":"knowledgeBase","message":"'cennik' is in the knowledge base 'x', which this bot does not have.","fix":"Use one of its knowledge bases: offer."}]}
            """
        );

        var result = await TestCli.Run(Args("articles", "add", "cennik", "--kb", "x"), server);

        Assert.Equal(1, result.ExitCode);
        foreach (
            var expected in new[]
            {
                "article.llmGuide.missing",
                "has no llmGuide.",
                "what to ask first",
                "article.knowledgeBase.unknown",
                "Use one of its knowledge bases: offer.",
            }
        )
        {
            Assert.Contains(expected, result.Error, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("criterion", "AC-articles-4")]
    public async Task A_file_is_sent_exactly_as_it_is_with_markdown_non_ascii_letters_crlf_a_byte_order_mark_and_trailing_newlines()
    {
        const string content = "# Cennik\r\n\r\n- zażółć gęślą jaźń \U0001F600\r\n\t`code`\n\n\n";
        var file = Scratch("cennik.md", [.. new UTF8Encoding(true).GetPreamble(), .. Encoding.UTF8.GetBytes(content)]);
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, PriceList));

        await TestCli.Run(Args("articles", "add", "cennik", "--content-file", file), server);

        Assert.Equal("﻿" + content, BodyOf(recorder.Calls[0]).GetProperty("content").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-articles-5")]
    public async Task Content_and_guide_come_from_files_from_stdin_or_from_an_option()
    {
        var file = Scratch("cennik.guide.md", Encoding.UTF8.GetBytes("Guide from a file\n"));
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, PriceList));

        var fromStdin = await TestCli.Run(
            Args("articles", "add", "cennik", "--content-file", "-", "--guide-file", file),
            server,
            stdin: Encoding.UTF8.GetBytes("Content from stdin\n\n")
        );
        var fromOption = await TestCli.Run(
            Args("articles", "add", "cennik", "--content", "Content from an option", "--guide", "g"),
            server
        );

        Assert.Equal([0, 0], [fromStdin.ExitCode, fromOption.ExitCode]);
        Assert.Equal("Content from stdin\n\n", BodyOf(recorder.Calls[0]).GetProperty("content").GetString());
        Assert.Equal("Guide from a file\n", BodyOf(recorder.Calls[0]).GetProperty("llmGuide").GetString());
        Assert.Equal("Content from an option", BodyOf(recorder.Calls[1]).GetProperty("content").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-articles-5")]
    public async Task A_text_given_twice_or_two_texts_from_stdin_or_a_file_that_is_missing_are_wrong_uses_and_nothing_is_sent()
    {
        var file = Scratch("x.md", Encoding.UTF8.GetBytes("x"));
        var server = new Recorder().Serving(_ => (HttpStatusCode.OK, PriceList));

        var twice = await TestCli.Run(Args("articles", "add", "a", "--content", "x", "--content-file", file), server);
        var bothStdin = await TestCli.Run(Args("articles", "add", "a", "--content-file", "-", "--guide-file", "-"), server, stdin: [65]);
        var missing = await TestCli.Run(Args("articles", "add", "a", "--content-file", file + ".nope"), server);

        Assert.Equal([2, 2, 2], [twice.ExitCode, bothStdin.ExitCode, missing.ExitCode]);
        Assert.Contains("not both", twice.Error, StringComparison.Ordinal);
        Assert.Contains("Only one text can be read from stdin", bothStdin.Error, StringComparison.Ordinal);
        Assert.Contains("Cannot read --content-file", missing.Error, StringComparison.Ordinal);
        Assert.Empty(server.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-articles-6")]
    public async Task Articles_edit_changes_only_the_options_given_and_keeps_every_other_field_as_it_was()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, PriceList));

        var result = await TestCli.Run(Args("articles", "edit", "priceList", "--title", "New title"), server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("GET", recorder.Calls[0].Method);
        Assert.Equal(("PUT", "/api/bots/rzeczoznawca/articles/priceList?ifRevision=3"), (recorder.Calls[1].Method, recorder.Calls[1].Url));
        Assert.Equal(
            """{"knowledgeBase":"offer","title":"New title","content":"# Price\n\nFrom 400 zl.\n","llmGuide":"Ask the kind first.","description":"What a valuation costs"}""",
            recorder.Calls[1].Body
        );
    }

    [Fact]
    [Trait("criterion", "AC-articles-6")]
    public async Task Articles_edit_with_a_new_kb_moves_the_article_and_a_file_replaces_only_that_text()
    {
        var file = Scratch("new.md", Encoding.UTF8.GetBytes("New content\n"));
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, PriceList));

        await TestCli.Run(Args("articles", "edit", "priceList", "--kb", "forValuers", "--content-file", file), server);

        var body = BodyOf(recorder.Calls[1]);
        Assert.Equal(
            ("forValuers", "New content\n", "Ask the kind first."),
            (
                body.GetProperty("knowledgeBase").GetString(),
                body.GetProperty("content").GetString(),
                body.GetProperty("llmGuide").GetString()
            )
        );
    }

    [Fact]
    [Trait("criterion", "AC-articles-6")]
    public async Task Articles_edit_with_no_option_to_change_is_a_wrong_use_and_asks_nothing()
    {
        var server = new Recorder().Serving(_ => (HttpStatusCode.OK, PriceList));

        var result = await TestCli.Run(Args("articles", "edit", "priceList"), server);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(server.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-articles-7")]
    public async Task If_revision_is_sent_as_given_on_an_edit_and_a_delete_and_a_stale_refusal_prints_the_current_revision()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(request =>
            request.Method == HttpMethod.Get
                ? (HttpStatusCode.OK, PriceList)
                : (
                    HttpStatusCode.Conflict,
                    """{"mistakes":[{"code":"resource.stale","path":"revision","message":"changed: it is now revision 3.","fix":"Read it again (revision 3)."}]}"""
                )
        );

        var edit = await TestCli.Run(Args("articles", "edit", "priceList", "--title", "x", "--if-revision", "1"), server);
        var delete = await TestCli.Run(Args("articles", "delete", "priceList", "--if-revision", "1"), server);

        Assert.Equal([1, 1], [edit.ExitCode, delete.ExitCode]);
        Assert.EndsWith("?ifRevision=1", recorder.Calls[1].Url, StringComparison.Ordinal);
        Assert.EndsWith("?ifRevision=1", recorder.Calls[2].Url, StringComparison.Ordinal);
        Assert.Contains("revision 3", edit.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-articles-7")]
    public async Task Dry_run_on_an_add_an_edit_and_a_delete_asks_for_a_dry_run_and_says_nothing_was_changed()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(request =>
            request.Method == HttpMethod.Delete ? (HttpStatusCode.NoContent, "") : (HttpStatusCode.OK, PriceList)
        );

        var add = await TestCli.Run(Args("articles", "add", "priceList", "--title", "t", "--dry-run"), server);
        var edit = await TestCli.Run(Args("articles", "edit", "priceList", "--title", "t", "--dry-run"), server);
        var delete = await TestCli.Run(Args("articles", "delete", "priceList", "--dry-run"), server);

        Assert.Equal("/api/bots/rzeczoznawca/articles/priceList?ifRevision=0&dryRun=true", recorder.Calls[0].Url);
        Assert.EndsWith("?ifRevision=3&dryRun=true", recorder.Calls[2].Url, StringComparison.Ordinal);
        Assert.EndsWith("?dryRun=true", recorder.Calls[3].Url, StringComparison.Ordinal);
        Assert.All([add, edit, delete], result => Assert.Contains("Nothing was changed.", result.Output, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("criterion", "AC-articles-8")]
    public async Task Articles_delete_deletes_the_named_article_and_says_so()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.NoContent, ""));

        var result = await TestCli.Run(Args("articles", "delete", "priceList"), server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(("DELETE", "/api/bots/rzeczoznawca/articles/priceList"), (recorder.Calls[0].Method, recorder.Calls[0].Url));
        Assert.Contains("Deleted 'priceList'", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-articles-11")]
    public async Task Articles_list_sends_the_base_and_the_text_as_filters()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, "[]"));

        await TestCli.Run(Args("articles", "list", "--kb", "offer", "--text", "rabat & more"), server);
        await TestCli.Run(Args("articles", "list", "--text", "rabat"), server);

        Assert.Equal("/api/bots/rzeczoznawca/articles?knowledgeBase=offer&text=rabat%20%26%20more", recorder.Calls[0].Url);
        Assert.Equal("/api/bots/rzeczoznawca/articles?text=rabat", recorder.Calls[1].Url);
    }

    [Fact]
    public async Task Open_shows_the_articles_screen_of_the_bot_without_calling_the_server()
    {
        var opened = new List<Uri>();
        var server = StubServer.Healthy();

        var result = await TestCli.Run(Args("articles", "list", "--open"), server, openBrowser: opened.Add);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://cms.internal/bots/rzeczoznawca/articles", Assert.Single(opened).ToString());
        Assert.Empty(server.Requests);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("edit")]
    [InlineData("delete")]
    public async Task A_command_that_changes_an_article_has_no_page_to_open(string word)
    {
        var result = await TestCli.Run(Args("articles", word, "priceList", "--title", "x", "--open"), StubServer.Healthy());

        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    [Trait("criterion", "AC-articles-14")]
    public void Every_articles_command_has_its_line_in_the_main_skill()
    {
        var skill = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", "suwagcms", "SKILL.md"));
        var commands = CommandList.RunnableInCli().Where(words => words.StartsWith("articles ", StringComparison.Ordinal)).ToList();

        Assert.Equal(["articles add", "articles delete", "articles edit", "articles list", "articles show"], commands);
        Assert.Empty(commands.Except(CommandList.Listed(skill)));
    }
}
