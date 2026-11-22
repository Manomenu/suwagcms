using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Cli.Tests.Plugin;

namespace Suwagcms.Cli.Tests.Procedures;

public sealed class ProceduresTests : IDisposable
{
    private static readonly string[] OnBot = ["--server", "http://cms.internal", "--bot", "rzeczoznawca"];

    private const string Yaml = """
        id: orderValuation
        description: The customer orders a valuation of a property.
        firstStep: wait
        steps:
          wait:
            listen:
            next: ask
          ask:
            collect: [customer.city, order.kind]
            next:
              - when: { order.kind: { oneOf: [house] }, order.floors: { range: { min: 10 } } }
                to: mark
              - when: { customer.city: { known: true } }
                to: confirm
              - to: goodbye
          mark:
            assign: { system.flag: "7", customer.city: Warsaw }
            next: confirm
          confirm:
            say: Thank you, we will call you.
            next: wait
          goodbye:
            say: "true"
        whenStalled:
          instruction: Tell the customer you could not finish and give the office phone number.
        """;

    private readonly string folder = Path.Combine(Path.GetTempPath(), "suwagcms-cli-tests", Guid.NewGuid().ToString("N"));

    public ProceduresTests()
    {
        Directory.CreateDirectory(folder);
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private sealed class Recorder
    {
        public List<(string Method, string Url, string Body)> Calls { get; } = [];

        public StubServer Serving(Func<HttpRequestMessage, string, (HttpStatusCode, string)> answer) =>
            new(request =>
            {
                using var reader = new StreamReader(request.Content?.ReadAsStream() ?? Stream.Null);
                var body = reader.ReadToEnd();
                Calls.Add((request.Method.Method, request.RequestUri?.PathAndQuery ?? "", body));
                var (status, json) = answer(request, body);
                return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            });
    }

    private static string[] Args(params string[] words) => [.. words, .. OnBot];

    private string FileWith(string name, string text)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, text);
        return path;
    }

    private static JsonObject Wire(string text)
    {
        var (document, problem) = Cli.Procedures.ProcedureFile.Parse(text, "test");
        return document ?? throw new InvalidOperationException(problem);
    }

    private static (HttpStatusCode, string) Echo(string sent, string id, int revision = 1, string notReady = "[]")
    {
        var body = JsonNode.Parse(sent) as JsonObject ?? [];
        var detail = new JsonObject { ["id"] = id };
        foreach (var (key, value) in body)
        {
            detail[key] = value?.DeepClone();
        }
        foreach (var field in new[] { "firstStep", "whenStalled" })
        {
            detail[field] ??= null;
        }
        foreach (var (_, step) in detail["steps"]?.AsObject() ?? [])
        {
            if (step is not JsonObject block)
            {
                continue;
            }
            foreach (var field in new[] { "listen", "collect", "say", "assign", "start", "inform", "next" })
            {
                block[field] ??= null;
            }
            foreach (var rule in block["next"] is JsonArray rules ? rules : [])
            {
                if (rule is JsonObject transition)
                {
                    transition["when"] ??= null;
                }
            }
        }
        detail["revision"] = revision;
        detail["notReady"] = JsonNode.Parse(notReady);
        return (HttpStatusCode.OK, detail.ToJsonString());
    }

    private static JsonNode At(JsonNode? node, params string[] path)
    {
        var current = node;
        foreach (var step in path)
        {
            current = current switch
            {
                JsonObject obj => obj[step],
                JsonArray array => array[int.Parse(step, System.Globalization.CultureInfo.InvariantCulture)],
                _ => null,
            };
        }
        return current ?? throw new InvalidOperationException($"nothing at {string.Join("/", path)}");
    }

    private static string Canonical(JsonNode? node) =>
        node switch
        {
            JsonObject obj => "{"
                + string.Join(
                    ",",
                    obj.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"\"{pair.Key}\":{Canonical(pair.Value)}")
                )
                + "}",
            JsonArray array => "[" + string.Join(",", array.Select(Canonical)) + "]",
            null => "null",
            _ => node.ToJsonString(),
        };

    [Fact]
    [Trait("criterion", "AC-chatbot-contract-5")]
    public async Task A_file_with_when_interrupted_is_added_and_edited_with_it_and_shown_with_it()
    {
        var file = FileWith("p.yaml", Yaml + "\nwhenInterrupted: discard\n");
        var recorder = new Recorder();
        var (_, stored) = Echo(Wire(Yaml).ToJsonString(), "orderValuation", 3);
        var server = recorder.Serving(
            (request, body) => request.Method == HttpMethod.Get ? (HttpStatusCode.OK, stored) : Echo(body, "orderValuation", 4)
        );

        var added = await TestCli.Run(Args("procedures", "add", "--file", file), server);
        var edited = await TestCli.Run(Args("procedures", "edit", "orderValuation", "--file", file), server);
        var shown = await TestCli.Run(
            Args("procedures", "show", "orderValuation"),
            new Recorder().Serving((_, _) => Echo(Wire(Yaml + "\nwhenInterrupted: discard\n").ToJsonString(), "orderValuation", 4))
        );

        Assert.Equal(0, added.ExitCode);
        Assert.Equal(0, edited.ExitCode);
        Assert.Equal(0, shown.ExitCode);
        var writes = recorder.Calls.Where(call => call.Method == "PUT").ToList();
        Assert.Equal(2, writes.Count);
        Assert.All(writes, write => Assert.Equal("discard", At(JsonNode.Parse(write.Body), "whenInterrupted").GetValue<string>()));
        Assert.Contains("whenInterrupted: discard", shown.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-1")]
    public async Task A_procedure_added_from_yaml_or_json_is_sent_with_its_steps_blocks_and_transitions_as_written()
    {
        var fromYaml = FileWith("p.yaml", Yaml);
        var asJson = FileWith("p.json", Wire(Yaml).ToJsonString());
        var recorder = new Recorder();
        var server = recorder.Serving((_, body) => Echo(body, "orderValuation"));

        var yaml = await TestCli.Run(Args("procedures", "add", "--file", fromYaml), server);
        var json = await TestCli.Run(Args("procedures", "add", "--file", asJson), server);

        Assert.Equal(0, yaml.ExitCode);
        Assert.Equal(0, json.ExitCode);
        Assert.Equal(
            ("PUT", "/api/bots/rzeczoznawca/procedures/orderValuation?ifRevision=0"),
            (recorder.Calls[0].Method, recorder.Calls[0].Url)
        );
        var sent = At(JsonNode.Parse(recorder.Calls[0].Body));
        Assert.Equal(Canonical(sent), Canonical(JsonNode.Parse(recorder.Calls[1].Body)));
        Assert.Null(sent["id"]);
        Assert.Equal("wait", At(sent, "firstStep").GetValue<string>());
        Assert.IsType<JsonObject>(At(sent, "steps", "wait", "listen"));
        Assert.Equal(
            ["customer.city", "order.kind"],
            At(sent, "steps", "ask", "collect").AsArray().Select(item => item?.GetValue<string>())
        );
        var first = At(sent, "steps", "ask", "next", "0", "when");
        Assert.Equal(["house"], At(first, "order.kind", "oneOf").AsArray().Select(item => item?.GetValue<string>()));
        Assert.Equal(10, At(first, "order.floors", "range", "min").GetValue<int>());
        Assert.True(At(sent, "steps", "ask", "next", "1", "when", "customer.city", "known").GetValue<bool>());
        Assert.Equal("7", At(sent, "steps", "mark", "assign", "system.flag").GetValue<string>());
        Assert.Equal("true", At(sent, "steps", "goodbye", "say").GetValue<string>());
        Assert.Contains("Added orderValuation, revision 1.", yaml.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-1")]
    public async Task Show_prints_what_was_saved_as_yaml_and_as_json()
    {
        var (_, stored) = Echo(Wire(Yaml).ToJsonString(), "orderValuation", 3);
        var showing = new Recorder().Serving((_, _) => (HttpStatusCode.OK, stored));

        var yaml = await TestCli.Run(Args("procedures", "show", "orderValuation"), showing);
        var json = await TestCli.Run([.. Args("procedures", "show", "orderValuation"), "--json"], showing);

        Assert.Equal(0, yaml.ExitCode);
        var expected = Wire(Yaml);
        expected["revision"] = 3;
        Assert.Equal(Canonical(expected), Canonical(Wire(yaml.Output)));
        Assert.Equal(Canonical(expected), Canonical(Strip(Wire(json.Output))));
    }

    private static JsonObject Strip(JsonObject document)
    {
        document.Remove("notReady");
        return document;
    }

    private const string Booking = """
        id: booking
        description: Books a valuation.
        firstStep: confirm
        steps:
          confirm:
            say: Booked for {customer.city}, reference {bookingReference}.
            next: repeat
          repeat:
            say: "{bookingReference} is your reference."
        localFacts:
          bookingReference: { type: text, description: The booking's reference, maxLength: 20 }
        """;

    [Theory]
    [Trait("criterion", "AC-procedure-model-18")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_say_showing_facts_is_sent_as_written_and_its_show_output_is_accepted_back(bool json)
    {
        var (_, stored) = Echo(Wire(Booking).ToJsonString(), "booking", 1);
        var recorder = new Recorder();
        var server = recorder.Serving(
            (request, body) => request.Method == HttpMethod.Get ? (HttpStatusCode.OK, stored) : Echo(body, "booking", 2)
        );

        var added = await TestCli.Run(Args("procedures", "add", "--file", FileWith("b.yaml", Booking)), server);
        var shown = await TestCli.Run(
            json ? [.. Args("procedures", "show", "booking"), "--json"] : Args("procedures", "show", "booking"),
            server
        );
        var edited = await TestCli.Run(
            Args("procedures", "edit", "booking", "--file", FileWith(json ? "shown.json" : "shown.yaml", shown.Output)),
            server
        );

        Assert.Equal((0, 0, 0), (added.ExitCode, shown.ExitCode, edited.ExitCode));
        var writes = recorder.Calls.Where(call => call.Method == "PUT").Select(call => JsonNode.Parse(call.Body)).ToList();
        Assert.Equal(2, writes.Count);
        Assert.All(writes, body =>
            Assert.Equal(
                ("Booked for {customer.city}, reference {bookingReference}.", "{bookingReference} is your reference."),
                (At(body, "steps", "confirm", "say").GetValue<string>(), At(body, "steps", "repeat", "say").GetValue<string>())
            )
        );
    }

    [Theory]
    [Trait("criterion", "AC-procedure-model-2")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_output_of_show_is_accepted_back_by_edit_without_a_change_of_meaning(bool json)
    {
        var (_, stored) = Echo(Wire(Yaml).ToJsonString(), "orderValuation", 4);
        var recorder = new Recorder();
        var server = recorder.Serving(
            (request, body) => request.Method == HttpMethod.Get ? (HttpStatusCode.OK, stored) : Echo(body, "orderValuation", 5)
        );

        var shown = await TestCli.Run(
            json ? [.. Args("procedures", "show", "orderValuation"), "--json"] : Args("procedures", "show", "orderValuation"),
            server
        );
        var file = FileWith(json ? "shown.json" : "shown.yaml", shown.Output);
        var (editedExit, editedOutput, _) = await TestCli.Run(Args("procedures", "edit", "orderValuation", "--file", file), server);

        Assert.Equal(0, editedExit);
        var (_, putUrl, putBody) = recorder.Calls.Last(call => call.Method == "PUT");
        Assert.Equal("/api/bots/rzeczoznawca/procedures/orderValuation?ifRevision=4", putUrl);
        var expectedBody = Wire(Yaml);
        expectedBody.Remove("id");
        Assert.Equal(Canonical(expectedBody), Canonical(JsonNode.Parse(putBody)));
        Assert.Contains("Saved orderValuation, revision 5.", editedOutput, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-3")]
    public async Task A_refusal_prints_every_mistake_with_its_code_message_and_fix_and_exits_1()
    {
        var file = FileWith("p.yaml", Yaml);
        var server = StubServer.Answering(
            HttpStatusCode.UnprocessableEntity,
            """
            {"mistakes":[
              {"code":"procedure.description.missing","path":"description","message":"'orderValuation' has no description.","fix":"Add one saying when the procedure is used."},
              {"code":"step.name.invalid","path":"steps.Say hi","message":"The step name 'Say hi' is not camelCase.","fix":"Rename the step to 'sayHi'."}]}
            """
        );

        var result = await TestCli.Run(Args("procedures", "add", "--file", file), server);

        Assert.Equal(1, result.ExitCode);
        foreach (
            var expected in new[]
            {
                "procedure.description.missing",
                "has no description.",
                "Add one saying",
                "step.name.invalid",
                "Rename the step to 'sayHi'.",
            }
        )
        {
            Assert.Contains(expected, result.Error, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-10")]
    public async Task A_save_with_only_version_mistakes_lists_them_and_exits_0()
    {
        var file = FileWith("p.yaml", Yaml);
        const string notReady = """
            [{"code":"procedure.collect.noWhenStalled","path":"whenStalled","message":"'orderValuation' has a collect step and no whenStalled.","fix":"Add whenStalled with an instruction.","stage":"Version","resource":{"kind":"Procedure","id":"orderValuation"},"related":[]},
             {"code":"step.unreachable","path":"steps.goodbye","message":"No path from firstStep reaches step 'goodbye'.","fix":"Connect it or delete it.","stage":"Version","resource":{"kind":"Procedure","id":"orderValuation"},"related":[]}]
            """;
        var server = new Recorder().Serving((_, body) => Echo(body, "orderValuation", 1, notReady));

        var result = await TestCli.Run(Args("procedures", "add", "--file", file), server);
        var json = await TestCli.Run([.. Args("procedures", "add", "--file", file), "--json"], server);

        Assert.Equal(0, result.ExitCode);
        var lines = result.Output.ReplaceLineEndings("\n").TrimEnd().Split('\n');
        Assert.Equal("Added orderValuation, revision 1. Not ready for a version, 2 mistakes:", lines[0]);
        Assert.Contains("procedure.collect.noWhenStalled", lines[1], StringComparison.Ordinal);
        Assert.Contains("Add whenStalled with an instruction.", lines[1], StringComparison.Ordinal);
        Assert.Contains("step.unreachable", lines[2], StringComparison.Ordinal);
        Assert.Equal(0, json.ExitCode);
        using var document = JsonDocument.Parse(json.Output);
        Assert.Equal(2, document.RootElement.GetProperty("procedure").GetProperty("notReady").GetArrayLength());
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-10")]
    public async Task Show_lists_what_stops_a_version_as_comments_so_the_text_is_still_a_file()
    {
        const string stored = """
            {"id":"p","description":"d","steps":{"a":{"say":"Hi"}},"revision":2,"notReady":[{"code":"procedure.firstStep.missing","path":"firstStep","message":"'p' has no firstStep.","fix":"Add 'firstStep: <step>'.","stage":"Version","resource":{"kind":"Procedure","id":"p"},"related":[]}]}
            """;

        var result = await TestCli.Run(Args("procedures", "show", "p"), StubServer.Answering(HttpStatusCode.OK, stored));

        Assert.Contains("# Not ready for a version, 1 mistake:", result.Output, StringComparison.Ordinal);
        Assert.Contains("#   procedure.firstStep.missing", result.Output, StringComparison.Ordinal);
        Assert.Equal("p", At(Wire(result.Output), "id").GetValue<string>());
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-12")]
    public async Task Deleting_conversation_prints_the_reason_and_exits_1_and_another_procedure_is_deleted()
    {
        var refused = StubServer.Answering(
            HttpStatusCode.UnprocessableEntity,
            """{"mistakes":[{"code":"procedure.conversation.required","path":"id","message":"'conversation' is the base of every conversation, and the format requires it, so it cannot be deleted.","fix":"Change what 'conversation' does with `procedures edit`, not whether it exists."}]}"""
        );
        var recorder = new Recorder();
        var allowed = recorder.Serving((_, _) => (HttpStatusCode.NoContent, ""));

        var no = await TestCli.Run(Args("procedures", "delete", "conversation"), refused);
        var yes = await TestCli.Run(Args("procedures", "delete", "orderValuation"), allowed);

        Assert.Equal(1, no.ExitCode);
        Assert.Contains("base of every conversation", no.Error, StringComparison.Ordinal);
        Assert.Equal(0, yes.ExitCode);
        Assert.Equal(("DELETE", "/api/bots/rzeczoznawca/procedures/orderValuation"), (recorder.Calls[0].Method, recorder.Calls[0].Url));
        Assert.Contains("Deleted 'orderValuation'", yes.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-13")]
    public async Task A_dry_run_asks_the_server_for_one_and_says_nothing_was_changed()
    {
        var file = FileWith("p.yaml", Yaml);
        var recorder = new Recorder();
        var server = recorder.Serving((_, body) => Echo(body, "orderValuation"));

        var add = await TestCli.Run(Args("procedures", "add", "--file", file, "--dry-run"), server);
        var delete = await TestCli.Run(
            Args("procedures", "delete", "orderValuation", "--dry-run"),
            recorder.Serving((_, _) => (HttpStatusCode.NoContent, ""))
        );

        Assert.Equal("/api/bots/rzeczoznawca/procedures/orderValuation?ifRevision=0&dryRun=true", recorder.Calls[0].Url);
        Assert.Contains("Would add 'orderValuation'. Nothing was changed.", add.Output, StringComparison.Ordinal);
        Assert.Equal("/api/bots/rzeczoznawca/procedures/orderValuation?dryRun=true", recorder.Calls[1].Url);
        Assert.Contains("Would delete 'orderValuation'. Nothing was changed.", delete.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-13")]
    public async Task An_edit_names_the_revision_it_is_based_on_and_a_stale_one_is_refused()
    {
        var file = FileWith("p.yaml", Yaml);
        var recorder = new Recorder();
        var server = recorder.Serving(
            (request, _) =>
                request.Method == HttpMethod.Get
                    ? (HttpStatusCode.OK, """{"id":"orderValuation","description":"d","steps":{},"revision":7,"notReady":[]}""")
                    : (
                        HttpStatusCode.Conflict,
                        """{"mistakes":[{"code":"resource.stale","path":"revision","message":"The procedure 'orderValuation' changed since you read it.","fix":"Read it again (revision 8)."}]}"""
                    )
        );

        var guarded = await TestCli.Run(Args("procedures", "edit", "orderValuation", "--file", file, "--if-revision", "5"), server);
        var latest = await TestCli.Run(Args("procedures", "edit", "orderValuation", "--file", file), server);

        Assert.Equal("/api/bots/rzeczoznawca/procedures/orderValuation?ifRevision=5", recorder.Calls[1].Url);
        Assert.Equal("/api/bots/rzeczoznawca/procedures/orderValuation?ifRevision=7", recorder.Calls[3].Url);
        Assert.Equal(1, guarded.ExitCode);
        Assert.Contains("Read it again (revision 8).", guarded.Error, StringComparison.Ordinal);
        Assert.Equal(1, latest.ExitCode);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-2")]
    public async Task A_file_whose_id_differs_from_the_command_is_refused_naming_both_before_the_server_is_asked()
    {
        var file = FileWith("p.yaml", Yaml);
        var server = StubServer.Healthy();

        var result = await TestCli.Run(Args("procedures", "edit", "conversation", "--file", file), server);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("'conversation'", result.Error, StringComparison.Ordinal);
        Assert.Contains("'orderValuation'", result.Error, StringComparison.Ordinal);
        Assert.Empty(server.Requests);
    }

    [Theory]
    [InlineData("id: [broken", "not valid YAML or JSON")]
    [InlineData("- a\n- b", "does not hold one procedure")]
    [InlineData("description: no id here", "has no id")]
    [Trait("criterion", "AC-procedure-model-2")]
    public async Task A_file_that_cannot_be_read_as_one_procedure_says_what_to_fix_and_asks_no_server(string text, string expected)
    {
        var file = FileWith("bad.yaml", text);
        var server = StubServer.Healthy();

        var result = await TestCli.Run(Args("procedures", "add", "--file", file), server);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(expected, result.Error, StringComparison.Ordinal);
        Assert.Empty(server.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-14")]
    public async Task List_prints_each_procedure_with_its_description_and_how_many_mistakes_stop_a_version()
    {
        const string list = """
            {"procedures":[
              {"id":"conversation","description":"The base of every conversation","revision":1,"notReady":0},
              {"id":"orderValuation","description":"The customer orders a valuation","revision":2,"notReady":2}],
             "botNotReady":[]}
            """;

        var result = await TestCli.Run(Args("procedures", "list"), StubServer.Answering(HttpStatusCode.OK, list));

        var lines = result.Output.ReplaceLineEndings("\n").TrimEnd().Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Contains("conversation", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("ready", lines[0], StringComparison.Ordinal);
        Assert.Contains("The customer orders a valuation", lines[1], StringComparison.Ordinal);
        Assert.EndsWith("2 mistakes before a version", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-14")]
    public async Task List_says_when_the_bot_has_no_conversation()
    {
        const string list = """
            {"procedures":[],"botNotReady":[{"code":"bot.conversation.missing","path":"procedures","message":"This bot has no 'conversation' procedure.","fix":"Add one.","stage":"Version","resource":{"kind":"Definition","id":""},"related":[]}]}
            """;

        var result = await TestCli.Run(Args("procedures", "list"), StubServer.Answering(HttpStatusCode.OK, list));

        Assert.Contains("No procedures yet", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-9")]
    public async Task Open_shows_the_procedures_screen_of_the_bot_without_calling_the_server()
    {
        var opened = new List<Uri>();
        var server = StubServer.Healthy();

        var result = await TestCli.Run(Args("procedures", "list", "--open"), server, openBrowser: opened.Add);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://cms.internal/bots/rzeczoznawca/procedures", Assert.Single(opened).ToString());
        Assert.Empty(server.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-creator-15")]
    public async Task Show_with_open_opens_the_creator_of_that_procedure_without_calling_the_server()
    {
        var opened = new List<Uri>();
        var server = StubServer.Healthy();

        var result = await TestCli.Run(Args("procedures", "show", "orderValuation", "--open"), server, openBrowser: opened.Add);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://cms.internal/bots/rzeczoznawca/procedures/orderValuation", Assert.Single(opened).ToString());
        Assert.Empty(server.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-creator-15")]
    public async Task Add_with_open_saves_and_then_opens_the_creator_but_a_refused_add_opens_nothing()
    {
        var opened = new List<Uri>();
        var recorder = new Recorder();
        var file = FileWith("p.yaml", Yaml);

        var result = await TestCli.Run(
            Args("procedures", "add", "--file", file, "--open"),
            recorder.Serving((_, sent) => Echo(sent, "orderValuation")),
            openBrowser: opened.Add
        );
        var refused = await TestCli.Run(
            Args("procedures", "add", "--file", file, "--open"),
            StubServer.Answering(HttpStatusCode.UnprocessableEntity, """{"detail":"no","mistakes":[]}"""),
            openBrowser: opened.Add
        );

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("PUT", Assert.Single(recorder.Calls).Method);
        Assert.NotEqual(0, refused.ExitCode);
        Assert.Equal("http://cms.internal/bots/rzeczoznawca/procedures/orderValuation", Assert.Single(opened).ToString());
    }

    [Fact]
    [Trait("criterion", "AC-procedure-creator-15")]
    public async Task Add_refuses_open_together_with_a_dry_run()
    {
        var result = await TestCli.Run(
            Args("procedures", "add", "--file", FileWith("p.yaml", Yaml), "--open", "--dry-run"),
            StubServer.Healthy()
        );

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--dry-run and --open", result.Error, StringComparison.Ordinal);
    }

    private const string StartYaml = """
        id: orderValuation
        description: The customer orders a valuation of a property.
        firstStep: getDocuments
        steps:
          getDocuments:
            start: documentsList
            next: confirm
          confirm:
            say: Thank you.
        """;

    [Fact]
    [Trait("criterion", "AC-start-block-1")]
    public async Task A_start_step_from_yaml_or_json_is_sent_as_written_and_show_is_accepted_back_by_edit()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(
            (request, body) =>
                request.Method == HttpMethod.Get ? Echo(Wire(StartYaml).ToJsonString(), "orderValuation") : Echo(body, "orderValuation", 2)
        );

        var yaml = await TestCli.Run(Args("procedures", "add", "--file", FileWith("p.yaml", StartYaml)), server);
        var json = await TestCli.Run(Args("procedures", "add", "--file", FileWith("p.json", Wire(StartYaml).ToJsonString())), server);
        var shown = await TestCli.Run(Args("procedures", "show", "orderValuation"), server);
        var edited = await TestCli.Run(Args("procedures", "edit", "orderValuation", "--file", FileWith("s.yaml", shown.Output)), server);

        Assert.Equal((0, 0, 0, 0), (yaml.ExitCode, json.ExitCode, shown.ExitCode, edited.ExitCode));
        var sent = JsonNode.Parse(recorder.Calls[0].Body);
        Assert.Equal("documentsList", At(sent, "steps", "getDocuments", "start").GetValue<string>());
        Assert.Equal("confirm", At(sent, "steps", "getDocuments", "next").GetValue<string>());
        Assert.Equal(Canonical(sent), Canonical(JsonNode.Parse(recorder.Calls[1].Body)));
        Assert.Equal(Canonical(sent), Canonical(JsonNode.Parse(recorder.Calls[^1].Body)));
    }

    [Fact]
    [Trait("criterion", "AC-start-block-5")]
    public async Task A_start_left_blank_in_yaml_is_sent_as_blank_saved_and_listed_as_a_version_mistake_with_exit_0()
    {
        const string blank = """
            id: orderValuation
            description: d
            firstStep: get
            steps:
              get:
                start:
            """;
        const string notReady = """
            [{"code":"step.block.empty","stage":"Version","resource":{"kind":"Procedure","id":"orderValuation"},"path":"steps.get.start",
              "message":"In step 'get', start names no procedure.","fix":"Write the id of the procedure to start: start: documentsList.","related":[]}]
            """;
        var recorder = new Recorder();
        var server = recorder.Serving((_, body) => Echo(body, "orderValuation", 1, notReady));

        var result = await TestCli.Run(Args("procedures", "add", "--file", FileWith("p.yaml", blank)), server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("", At(JsonNode.Parse(recorder.Calls[0].Body), "steps", "get", "start").GetValue<string>());
        Assert.Contains("step.block.empty", result.Output, StringComparison.Ordinal);
    }

    private const string InformYaml = """
        id: orderValuation
        description: The customer orders a valuation of a property.
        firstStep: whatHappensNext
        steps:
          whatHappensNext:
            inform:
              instruction: Tell the customer where to bring the documents.
              knowledge:
                - knowledgeBase: offer
                  articles: [zebraRules, officeContact]
              points:
                turnaround: The report is ready within 7 working days.
                delivery: 5
            next: wait
          wait:
            listen:
        """;

    [Fact]
    [Trait("criterion", "AC-inform-block-1")]
    public async Task An_inform_step_from_yaml_or_json_is_sent_as_written_and_show_is_accepted_back_by_edit()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(
            (request, body) =>
                request.Method == HttpMethod.Get ? Echo(Wire(InformYaml).ToJsonString(), "orderValuation") : Echo(body, "orderValuation", 2)
        );

        var yaml = await TestCli.Run(Args("procedures", "add", "--file", FileWith("p.yaml", InformYaml)), server);
        var json = await TestCli.Run(Args("procedures", "add", "--file", FileWith("p.json", Wire(InformYaml).ToJsonString())), server);
        var shown = await TestCli.Run(Args("procedures", "show", "orderValuation"), server);
        var edited = await TestCli.Run(Args("procedures", "edit", "orderValuation", "--file", FileWith("s.yaml", shown.Output)), server);

        Assert.Equal((0, 0, 0, 0), (yaml.ExitCode, json.ExitCode, shown.ExitCode, edited.ExitCode));
        var sent = JsonNode.Parse(recorder.Calls[0].Body);
        var inform = At(sent, "steps", "whatHappensNext", "inform");
        Assert.Equal("Tell the customer where to bring the documents.", At(inform, "instruction").GetValue<string>());
        Assert.Equal(
            ["zebraRules", "officeContact"],
            At(At(inform, "knowledge").AsArray()[0], "articles").AsArray().Select(item => item?.GetValue<string>())
        );
        Assert.Equal(["turnaround", "delivery"], At(inform, "points").AsObject().Select(point => point.Key));
        Assert.Equal("5", At(inform, "points", "delivery").GetValue<string>());
        Assert.Equal(Canonical(sent), Canonical(JsonNode.Parse(recorder.Calls[1].Body)));
        Assert.Equal(Canonical(sent), Canonical(JsonNode.Parse(recorder.Calls[^1].Body)));
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-6")]
    public async Task An_inform_with_nothing_filled_in_is_sent_with_a_blank_instruction_and_its_mistakes_exit_0()
    {
        const string blank = """
            id: orderValuation
            description: d
            firstStep: tell
            steps:
              tell:
                inform:
                  instruction:
            """;
        const string notReady = """
            [{"code":"step.inform.empty","stage":"Version","resource":{"kind":"Procedure","id":"orderValuation"},"path":"steps.tell.inform",
              "message":"'inform' of step 'tell' has no points.","fix":"List what the customer must hear: points: { name: what must be said }.","related":[]},
             {"code":"instruction.blank","stage":"Version","resource":{"kind":"Procedure","id":"orderValuation"},"path":"steps.tell.inform.instruction",
              "message":"'inform' of step 'tell' has no instruction.","fix":"Say what the agent should do with these points.","related":[]}]
            """;
        var recorder = new Recorder();
        var server = recorder.Serving((_, body) => Echo(body, "orderValuation", 1, notReady));

        var result = await TestCli.Run(Args("procedures", "add", "--file", FileWith("p.yaml", blank)), server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("", At(JsonNode.Parse(recorder.Calls[0].Body), "steps", "tell", "inform", "instruction").GetValue<string>());
        Assert.Contains("step.inform.empty", result.Output, StringComparison.Ordinal);
        Assert.Contains("instruction.blank", result.Output, StringComparison.Ordinal);
    }

    private const string KnowledgeYaml = """
        id: orderValuation
        description: The customer orders a valuation of a property.
        firstStep: tell
        steps:
          tell:
            inform:
              instruction: Tell the customer how to reach the office.
              points:
                office: Where the office is.
              knowledge:
                - knowledgeBase: offer
                  articles: [officeContact]
                  searchQueries:
                    - how long a valuation takes
                    - "42"
        """;

    [Fact]
    [Trait("criterion", "AC-instruction-context-1")]
    public async Task An_inform_with_knowledge_from_yaml_or_json_is_sent_as_written_and_show_is_accepted_back_by_edit()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(
            (request, body) =>
                request.Method == HttpMethod.Get
                    ? Echo(Wire(KnowledgeYaml).ToJsonString(), "orderValuation")
                    : Echo(body, "orderValuation", 2)
        );

        var yaml = await TestCli.Run(Args("procedures", "add", "--file", FileWith("p.yaml", KnowledgeYaml)), server);
        var json = await TestCli.Run(Args("procedures", "add", "--file", FileWith("p.json", Wire(KnowledgeYaml).ToJsonString())), server);
        var shown = await TestCli.Run(Args("procedures", "show", "orderValuation"), server);
        var edited = await TestCli.Run(Args("procedures", "edit", "orderValuation", "--file", FileWith("s.yaml", shown.Output)), server);

        Assert.Equal((0, 0, 0, 0), (yaml.ExitCode, json.ExitCode, shown.ExitCode, edited.ExitCode));
        var sent = JsonNode.Parse(recorder.Calls[0].Body);
        var entry = At(sent, "steps", "tell", "inform", "knowledge").AsArray()[0];
        Assert.Equal("offer", At(entry, "knowledgeBase").GetValue<string>());
        Assert.Equal(["officeContact"], At(entry, "articles").AsArray().Select(item => item?.GetValue<string>()));
        Assert.Equal(["how long a valuation takes", "42"], At(entry, "searchQueries").AsArray().Select(item => item?.GetValue<string>()));
        Assert.Equal(Canonical(sent), Canonical(JsonNode.Parse(recorder.Calls[1].Body)));
        Assert.Equal(Canonical(sent), Canonical(JsonNode.Parse(recorder.Calls[^1].Body)));
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-5")]
    public async Task A_blank_query_saved_with_the_cli_is_listed_as_a_version_mistake_with_exit_0()
    {
        const string notReady = """
            [{"code":"knowledge.query.blank","stage":"Version","resource":{"kind":"Procedure","id":"orderValuation"},"path":"steps.tell.inform.knowledge[0].searchQueries[0]",
              "message":"'knowledge' of step 'tell' in 'orderValuation' has a blank search query.","fix":"Write the query, or remove it.","related":[]}]
            """;
        var server = new Recorder().Serving((_, body) => Echo(body, "orderValuation", 1, notReady));

        var result = await TestCli.Run(Args("procedures", "add", "--file", FileWith("p.yaml", KnowledgeYaml)), server);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("knowledge.query.blank", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-model-15")]
    public void The_main_skill_gives_the_file_format_of_a_procedure()
    {
        var skill = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", "suwagcms", "SKILL.md"));
        var section = SkillText.Section(skill, "Procedure file");

        Assert.Contains("id: conversation", section, StringComparison.Ordinal);
        Assert.Contains("firstStep: wait", section, StringComparison.Ordinal);
        foreach (
            var word in new[]
            {
                "`listen`",
                "`collect: ",
                "`say: ",
                "`assign: ",
                "`next`",
                "`when`",
                "`oneOf: ",
                "`range: ",
                "`known: ",
                "`whenStalled: ",
                "`localFacts`",
            }
        )
        {
            Assert.Contains(word, section, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("criterion", "AC-global-facts-15")]
    public async Task Facts_show_names_the_procedures_that_use_the_fact_or_says_none_does()
    {
        const string used =
            """{"name":"order.kind","type":"enum","description":"What","writableBy":"set","values":["a","b"],"revision":1,"usedBy":["conversation","orderValuation"]}""";
        const string unused =
            """{"name":"order.kind","type":"enum","description":"What","writableBy":"set","values":["a","b"],"revision":1,"usedBy":[]}""";

        var with = await TestCli.Run(Args("facts", "show", "order.kind"), StubServer.Answering(HttpStatusCode.OK, used));
        var without = await TestCli.Run(Args("facts", "show", "order.kind"), StubServer.Answering(HttpStatusCode.OK, unused));

        Assert.Contains("used by:     conversation, orderValuation", with.Output, StringComparison.Ordinal);
        Assert.Contains("used by:     no procedure", without.Output, StringComparison.Ordinal);
    }
}
