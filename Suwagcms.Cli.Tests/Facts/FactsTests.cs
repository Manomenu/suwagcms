using System.CommandLine;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Suwagcms.Cli.Tests.Facts;

public sealed class FactsTests
{
    private static readonly string[] OnBot = ["--server", "http://cms.internal", "--bot", "rzeczoznawca"];

    private const string City =
        """{"name":"customer.city","type":"text","description":"The city","writableBy":"set","maxLength":80,"revision":3}""";

    private const string Kind =
        """{"name":"order.kind","type":"enum","description":"What","writableBy":"correct","values":["house","flat"],"revision":1}""";

    private const string Floors =
        """{"name":"order.floors","type":"int","description":"Floors","writableBy":"assign","min":1,"max":40,"revision":1}""";

    private sealed class Recorder
    {
        public List<(string Method, string Url, string Body)> Calls { get; } = [];

        public StubServer Serving(Func<HttpRequestMessage, (HttpStatusCode, string)> answer) =>
            new(request =>
            {
                using var reader = new StreamReader(request.Content?.ReadAsStream() ?? Stream.Null);
                var body = reader.ReadToEnd();
                Calls.Add((request.Method.Method, request.RequestUri?.PathAndQuery ?? "", body));
                var (status, json) = answer(request);
                return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            });
    }

    private const string Coded =
        """{"name":"order.code","type":"text","description":"The code","writableBy":"set","maxLength":10,"pattern":{"regex":"[A-Z]{2}","means":"two capitals","example":"AB","tellCustomer":"afterRefusal"},"revision":3,"usedBy":[]}""";

    private static string[] Args(params string[] words) => [.. words, .. OnBot];

    private static JsonElement PatternSent(Recorder recorder) =>
        JsonDocument.Parse(recorder.Calls[^1].Body).RootElement.GetProperty("pattern");

    [Fact]
    [Trait("criterion", "AC-fact-pattern-12")]
    public async Task Facts_add_sends_the_pattern_with_upfront_by_default_and_show_prints_it()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, Coded));

        var added = await TestCli.Run(
            Args(
                "facts",
                "add",
                "order.code",
                "--type",
                "text",
                "--max-length",
                "10",
                "--description",
                "The code",
                "--pattern-regex",
                "[A-Z]{2}",
                "--pattern-means",
                "two capitals"
            ),
            server
        );
        var pattern = PatternSent(recorder);
        var shown = await TestCli.Run(Args("facts", "show", "order.code"), server);

        Assert.Equal(0, added.ExitCode);
        Assert.Equal("[A-Z]{2}", pattern.GetProperty("regex").GetString());
        Assert.Equal("two capitals", pattern.GetProperty("means").GetString());
        Assert.Equal("upfront", pattern.GetProperty("tellCustomer").GetString());
        Assert.Contains("pattern [A-Z]{2}", added.Output, StringComparison.Ordinal);
        Assert.Contains("means:     two capitals", shown.Output, StringComparison.Ordinal);
        Assert.Contains("example:   AB", shown.Output, StringComparison.Ordinal);
        Assert.Contains("tell:      afterRefusal", shown.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-12")]
    public async Task Facts_edit_keeps_a_pattern_it_was_not_told_about_and_changes_only_the_part_given()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, Coded));

        await TestCli.Run(Args("facts", "edit", "order.code", "--description", "New"), server);
        var kept = PatternSent(recorder);
        await TestCli.Run(Args("facts", "edit", "order.code", "--pattern-means", "two big letters", "--pattern-example", ""), server);
        var changed = PatternSent(recorder);

        Assert.Equal("two capitals", kept.GetProperty("means").GetString());
        Assert.Equal("AB", kept.GetProperty("example").GetString());
        Assert.Equal("[A-Z]{2}", changed.GetProperty("regex").GetString());
        Assert.Equal("two big letters", changed.GetProperty("means").GetString());
        Assert.Equal("afterRefusal", changed.GetProperty("tellCustomer").GetString());
        Assert.False(changed.TryGetProperty("example", out _));
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-12")]
    public async Task Facts_add_sends_the_telling_it_is_given()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, Coded));

        await TestCli.Run(
            Args(
                "facts",
                "add",
                "order.code",
                "--type",
                "text",
                "--max-length",
                "10",
                "--description",
                "The code",
                "--pattern-regex",
                "[A-Z]{2}",
                "--pattern-means",
                "two capitals",
                "--pattern-tell-customer",
                "afterRefusal"
            ),
            server
        );

        Assert.Equal("afterRefusal", PatternSent(recorder).GetProperty("tellCustomer").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-12")]
    public async Task Facts_edit_sends_a_new_telling_or_a_new_regex_with_the_rest_of_the_pattern_kept()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, Coded));

        await TestCli.Run(Args("facts", "edit", "order.code", "--pattern-tell-customer", "upfront"), server);
        var told = PatternSent(recorder).Clone();
        await TestCli.Run(Args("facts", "edit", "order.code", "--pattern-regex", "[A-Z]{1,2}"), server);
        var regexed = PatternSent(recorder).Clone();

        Assert.Equal("upfront", told.GetProperty("tellCustomer").GetString());
        Assert.Equal("[A-Z]{2}", told.GetProperty("regex").GetString());
        Assert.Equal("two capitals", told.GetProperty("means").GetString());
        Assert.Equal("AB", told.GetProperty("example").GetString());
        Assert.Equal("[A-Z]{1,2}", regexed.GetProperty("regex").GetString());
        Assert.Equal("two capitals", regexed.GetProperty("means").GetString());
        Assert.Equal("AB", regexed.GetProperty("example").GetString());
        Assert.Equal("afterRefusal", regexed.GetProperty("tellCustomer").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-fact-pattern-12")]
    public async Task No_pattern_takes_it_off_a_change_of_type_drops_it_and_no_pattern_with_a_pattern_option_is_refused()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, Coded));

        var removed = await TestCli.Run(Args("facts", "edit", "order.code", "--no-pattern"), server);
        var none = PatternSent(recorder).ValueKind;
        await TestCli.Run(Args("facts", "edit", "order.code", "--type", "int", "--min", "1", "--max", "2"), server);
        var retyped = PatternSent(recorder).ValueKind;
        var calls = recorder.Calls.Count;
        var both = await TestCli.Run(Args("facts", "edit", "order.code", "--no-pattern", "--pattern-regex", "a"), server);

        Assert.Equal(0, removed.ExitCode);
        Assert.Equal(JsonValueKind.Null, none);
        Assert.Equal(JsonValueKind.Null, retyped);
        Assert.NotEqual(0, both.ExitCode);
        Assert.Contains("--no-pattern", both.Error, StringComparison.Ordinal);
        Assert.Equal(calls, recorder.Calls.Count);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-1")]
    public async Task Facts_list_prints_every_fact_with_its_type_constraint_writableBy_and_description()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, $"[{City},{Kind},{Floors}]"));

        var result = await TestCli.Run(Args("facts", "list"), server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("GET /api/bots/rzeczoznawca/facts", $"{recorder.Calls[0].Method} {recorder.Calls[0].Url}");
        var lines = result.Output.ReplaceLineEndings("\n").TrimEnd().Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.Contains("customer.city", lines[0], StringComparison.Ordinal);
        Assert.Contains("text, up to 80", lines[0], StringComparison.Ordinal);
        Assert.Contains("set", lines[0], StringComparison.Ordinal);
        Assert.Contains("The city", lines[0], StringComparison.Ordinal);
        Assert.Contains("enum: house, flat", lines[1], StringComparison.Ordinal);
        Assert.Contains("correct", lines[1], StringComparison.Ordinal);
        Assert.Contains("int, 1 to 40", lines[2], StringComparison.Ordinal);
        Assert.Contains("assign", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-1")]
    public async Task Facts_show_prints_one_fact_in_full_and_json_gives_its_fields_and_revision()
    {
        var server = new Recorder().Serving(_ => (HttpStatusCode.OK, Kind));

        var text = await TestCli.Run(Args("facts", "show", "order.kind"), server);
        var json = await TestCli.Run([.. Args("facts", "show", "order.kind"), "--json"], server);

        Assert.Contains("enum: house, flat", text.Output, StringComparison.Ordinal);
        Assert.Contains("writableBy:  correct", text.Output, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json.Output);
        Assert.Equal("order.kind", document.RootElement.GetProperty("name").GetString());
        Assert.Equal(1, document.RootElement.GetProperty("revision").GetInt32());
        Assert.Equal(["house", "flat"], document.RootElement.GetProperty("values").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-1")]
    public async Task Facts_add_sends_the_fields_as_typed_and_must_not_exist_yet()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, Kind));

        var result = await TestCli.Run(
            Args(
                "facts",
                "add",
                "order.kind",
                "--type",
                "enum",
                "--description",
                "What",
                "--values",
                "house",
                "flat",
                "--writable-by",
                "correct"
            ),
            server
        );

        Assert.Equal(0, result.ExitCode);
        var (method, url, body) = Assert.Single(recorder.Calls);
        Assert.Equal(("PUT", "/api/bots/rzeczoznawca/facts/order.kind?ifRevision=0"), (method, url));
        Assert.Equal(
            """{"type":"enum","description":"What","writableBy":"correct","values":["house","flat"],"min":null,"max":null,"maxLength":null,"pattern":null}""",
            body
        );
        Assert.Contains("Added 'order.kind'", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-6")]
    public async Task A_refused_add_prints_every_mistake_with_its_code_message_and_fix_and_exits_1()
    {
        var server = StubServer.Answering(
            HttpStatusCode.UnprocessableEntity,
            """
            {"mistakes":[
              {"code":"fact.description.missing","path":"description","message":"'order.kind' has no description.","fix":"Add one: the model reads it to understand what the fact means."},
              {"code":"fact.enum.tooFewValues","path":"values","message":"'order.kind' has 1 value.","fix":"An enum needs at least two values: add another."}]}
            """
        );

        var result = await TestCli.Run(Args("facts", "add", "order.kind", "--type", "enum", "--values", "house"), server);

        Assert.Equal(1, result.ExitCode);
        foreach (
            var expected in new[]
            {
                "fact.description.missing",
                "has no description.",
                "the model reads it",
                "fact.enum.tooFewValues",
                "add another.",
            }
        )
        {
            Assert.Contains(expected, result.Error, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-8")]
    public async Task Facts_delete_deletes_the_named_fact_and_says_so()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.NoContent, ""));

        var result = await TestCli.Run(Args("facts", "delete", "customer.city"), server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(("DELETE", "/api/bots/rzeczoznawca/facts/customer.city"), (recorder.Calls[0].Method, recorder.Calls[0].Url));
        Assert.Contains("Deleted 'customer.city'", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-8")]
    public async Task Deleting_a_fact_that_does_not_exist_prints_the_mistake_naming_it_and_exits_1()
    {
        var server = StubServer.Answering(
            HttpStatusCode.NotFound,
            """{"mistakes":[{"code":"resource.missing","path":"name","message":"Bot 'rzeczoznawca' has no fact 'customer.city'.","fix":"Check the name."}]}"""
        );

        var result = await TestCli.Run(Args("facts", "delete", "customer.city"), server);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("'customer.city'", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-9")]
    public async Task Facts_edit_changes_only_the_options_given_and_keeps_every_other_field_as_it_was()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, City));

        var result = await TestCli.Run(Args("facts", "edit", "customer.city", "--description", "Where they live"), server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("GET", recorder.Calls[0].Method);
        Assert.Equal("PUT", recorder.Calls[1].Method);
        Assert.Equal(
            """{"type":"text","description":"Where they live","writableBy":"set","values":null,"min":null,"max":null,"maxLength":80,"pattern":null}""",
            recorder.Calls[1].Body
        );
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-9")]
    public async Task Changing_the_type_of_a_fact_drops_the_constraints_of_the_old_type()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, City));

        await TestCli.Run(Args("facts", "edit", "customer.city", "--type", "int", "--min", "1", "--max", "9"), server);

        Assert.Equal(
            """{"type":"int","description":"The city","writableBy":"set","values":null,"min":1,"max":9,"maxLength":null,"pattern":null}""",
            recorder.Calls[1].Body
        );
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-9")]
    public async Task Facts_edit_with_no_option_to_change_is_a_wrong_use_and_asks_nothing()
    {
        var server = new Recorder().Serving(_ => (HttpStatusCode.OK, City));

        var result = await TestCli.Run(Args("facts", "edit", "customer.city"), server);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(server.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-10")]
    public async Task Dry_run_asks_the_server_for_a_dry_run_and_says_nothing_was_changed()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.NoContent, ""));

        var result = await TestCli.Run(Args("facts", "delete", "customer.city", "--dry-run"), server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("/api/bots/rzeczoznawca/facts/customer.city?dryRun=true", recorder.Calls[0].Url);
        Assert.Equal("Would delete 'customer.city'. Nothing was changed.", result.Output.Trim());
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-10")]
    public async Task Dry_run_of_an_add_and_an_edit_ask_for_a_dry_run_and_json_says_it_was_one()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, City));

        var add = await TestCli.Run(
            [.. Args("facts", "add", "customer.city", "--type", "text", "--description", "d", "--max-length", "3", "--dry-run"), "--json"],
            server
        );
        var edit = await TestCli.Run(Args("facts", "edit", "customer.city", "--description", "d", "--dry-run"), server);

        Assert.Equal("/api/bots/rzeczoznawca/facts/customer.city?ifRevision=0&dryRun=true", recorder.Calls[0].Url);
        Assert.EndsWith("?ifRevision=3&dryRun=true", recorder.Calls[2].Url, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(add.Output);
        Assert.True(document.RootElement.GetProperty("dryRun").GetBoolean());
        Assert.Contains("Would edit 'customer.city'", edit.Output, StringComparison.Ordinal);
        Assert.Contains("Nothing was changed.", edit.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-11")]
    public async Task If_revision_is_sent_as_given_and_a_stale_refusal_prints_the_current_revision()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(request =>
            request.Method == HttpMethod.Get
                ? (HttpStatusCode.OK, City)
                : (
                    HttpStatusCode.Conflict,
                    """{"mistakes":[{"code":"resource.stale","path":"revision","message":"changed: it is now revision 3.","fix":"Read it again (revision 3)."}]}"""
                )
        );

        var result = await TestCli.Run(Args("facts", "edit", "customer.city", "--description", "x", "--if-revision", "1"), server);

        Assert.Equal(1, result.ExitCode);
        Assert.EndsWith("?ifRevision=1", recorder.Calls[1].Url, StringComparison.Ordinal);
        Assert.Contains("revision 3", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-11")]
    public async Task Delete_with_if_revision_sends_it()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.NoContent, ""));

        await TestCli.Run(Args("facts", "delete", "customer.city", "--if-revision", "2"), server);

        Assert.EndsWith("?ifRevision=2", recorder.Calls[0].Url, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-10")]
    public async Task With_no_bot_chosen_a_facts_command_exits_2_and_lists_the_bots_without_touching_facts()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(_ => (HttpStatusCode.OK, """[{"id":"rzeczoznawca","name":"R","address":"http://r"}]"""));

        var result = await TestCli.Run(["facts", "list", "--server", "http://cms.internal"], server);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("rzeczoznawca", result.Error, StringComparison.Ordinal);
        Assert.Equal(["/api/bots"], recorder.Calls.Select(call => call.Url));
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-10")]
    public async Task A_bot_the_server_does_not_know_exits_1_and_lists_the_bots_that_exist()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(request =>
            request.RequestUri?.AbsolutePath == "/api/bots"
                ? (HttpStatusCode.OK, """[{"id":"sklep","name":"S","address":"http://s"}]""")
                : (
                    HttpStatusCode.NotFound,
                    """{"mistakes":[{"code":"bot.notFound","path":"bot","message":"There is no bot 'rzeczoznawca'.","fix":"Check the bot's id."}]}"""
                )
        );

        var result = await TestCli.Run(Args("facts", "list"), server);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("There is no bot", result.Error, StringComparison.Ordinal);
        Assert.Contains("Bots: sklep.", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-9")]
    public async Task Open_shows_the_facts_screen_of_the_bot_without_calling_the_server()
    {
        var opened = new List<Uri>();
        var server = StubServer.Healthy();

        var result = await TestCli.Run(Args("facts", "list", "--open"), server, openBrowser: opened.Add);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://cms.internal/bots/rzeczoznawca/facts", Assert.Single(opened).ToString());
        Assert.Empty(server.Requests);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("edit")]
    [InlineData("delete")]
    [Trait("criterion", "AC-cli-foundation-9")]
    public async Task A_command_that_changes_a_fact_has_no_page_to_open(string word)
    {
        var result = await TestCli.Run(Args("facts", word, "customer.city", "--open"), StubServer.Healthy());

        Assert.Equal(2, result.ExitCode);
    }
}
