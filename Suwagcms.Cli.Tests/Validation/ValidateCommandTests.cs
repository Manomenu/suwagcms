using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Suwagcms.Cli.Tests.Validation;

public sealed class ValidateCommandTests : IDisposable
{
    private const string Mistake =
        """{"code":"declaration.formatVersion","stage":"Version","resource":{"kind":"Definition","id":""},"path":"formatVersion","message":"This definition is format 5 and the bot wants format 4.","fix":"Update Suwagcms.Sdk on the CMS, or declare format 5 in the bot.","related":[]}""";

    private static readonly string[] Common = ["--server", "http://cms.internal"];

    private readonly string folder = Directory.CreateTempSubdirectory("suwagcms-validate-tests").FullName;

    private readonly List<JsonNode> sent = [];

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private string File(string name, string text)
    {
        var path = Path.Combine(folder, name);
        System.IO.File.WriteAllText(path, text);
        return path;
    }

    private StubServer Answering(string mistakes) =>
        new(request =>
        {
            Assert.Equal("/api/validate", request.RequestUri?.AbsolutePath);
            using var body = new StreamReader(request.Content?.ReadAsStream() ?? Stream.Null);
            sent.Add(JsonNode.Parse(body.ReadToEnd()) ?? new JsonObject());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""{"mistakes":[{{mistakes}}]}""", Encoding.UTF8, "application/json"),
            };
        });

    [Fact]
    [Trait("criterion", "AC-bot-declaration-15")]
    public async Task It_reads_json_checks_the_version_stage_unless_told_otherwise_and_exits_0_for_no_mistakes()
    {
        var path = File("definition.json", """{"formatVersion":5,"facts":[]}""");

        var result = await TestCli.Run(["validate", "--file", path, .. Common], Answering(""));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("No mistakes at the Version stage.", result.Output, StringComparison.Ordinal);
        Assert.Equal("Version", sent[0]["stage"]?.GetValue<string>());
        Assert.Equal(5, sent[0]["definition"]?["formatVersion"]?.GetValue<int>());
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-15")]
    public async Task It_reads_yaml_with_numbers_and_texts_as_the_format_means_them_and_takes_a_stage_and_a_declaration()
    {
        var definition = File(
            "definition.yaml",
            """
            formatVersion: 5
            facts:
              - name: floors
                type: int
                description: "42"
                min: 1
                max: 9
              - name: kind
                type: enum
                description: Kind
                values: [1, true]
            procedures:
              - id: conversation
                steps:
                  wait:
                    listen:
                    next:
                      - when: { floors: { known: true } }
                        to: wait
                  set:
                    assign: { min: 42 }
            """
        );
        var declaration = File("declaration.yaml", "bot: x\nformatVersion: 4\nblocks: [listen]\ndeclaresNothing: false\n");

        var result = await TestCli.Run(
            ["validate", "--file", definition, "--declaration", declaration, "--stage", "save", .. Common],
            Answering("")
        );

        Assert.Equal(0, result.ExitCode);
        var body = sent[0];
        Assert.Equal("Save", body["stage"]?.GetValue<string>());
        Assert.Equal(5, body["definition"]?["formatVersion"]?.GetValue<int>());
        var facts = body["definition"]?["facts"];
        Assert.Equal(9, facts?[0]?["max"]?.GetValue<int>());
        Assert.Equal("42", facts?[0]?["description"]?.GetValue<string>());
        Assert.Equal("""["1","true"]""", facts?[1]?["values"]?.ToJsonString());
        var steps = body["definition"]?["procedures"]?[0]?["steps"];
        Assert.Equal("{}", steps?["wait"]?["listen"]?.ToJsonString());
        Assert.True(steps?["wait"]?["next"]?[0]?["when"]?["floors"]?["known"]?.GetValue<bool>());
        Assert.Equal("42", steps?["set"]?["assign"]?["min"]?.GetValue<string>());
        Assert.Equal(4, body["declaration"]?["formatVersion"]?.GetValue<int>());
        Assert.False(body["declaration"]?["declaresNothing"]?.GetValue<bool>());
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-15")]
    public async Task An_actions_timeout_reads_as_the_number_an_actions_file_gives_it()
    {
        var path = File(
            "definition.yaml",
            """
            formatVersion: 5
            actions:
              - id: priceLookup
                description: Looks up a price.
                implementation:
                  http: { method: GET, url: "http://x.internal/a", timeoutSeconds: 20 }
            """
        );

        await TestCli.Run(["validate", "--file", path, .. Common], Answering(""));

        Assert.Equal(20, sent[0]["definition"]?["actions"]?[0]?["implementation"]?["http"]?["timeoutSeconds"]?.GetValue<int>());
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-15")]
    [Trait("criterion", "AC-start-block-5")]
    public async Task A_start_or_an_instruction_left_empty_reads_as_blank_as_in_a_procedures_file()
    {
        var path = File(
            "definition.yaml",
            """
            formatVersion: 5
            procedures:
              - id: conversation
                steps:
                  go:
                    start:
                    next: go
                whenStalled:
                  instruction:
            """
        );

        await TestCli.Run(["validate", "--file", path, .. Common], Answering(""));

        var procedure = sent[0]["definition"]?["procedures"]?[0];
        Assert.Equal("", procedure?["steps"]?["go"]?["start"]?.GetValue<string>());
        Assert.Equal("", procedure?["whenStalled"]?["instruction"]?.GetValue<string>());
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-15")]
    public async Task A_field_named_like_a_number_inside_a_steps_mapping_stays_the_text_it_spells()
    {
        var path = File(
            "definition.yaml",
            """
            formatVersion: 5
            procedures:
              - id: conversation
                steps:
                  go:
                    action: lookup
                    with: { min: 5 }
            """
        );

        await TestCli.Run(["validate", "--file", path, .. Common], Answering(""));

        Assert.Equal("5", sent[0]["definition"]?["procedures"]?[0]?["steps"]?["go"]?["with"]?["min"]?.GetValue<string>());
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-15")]
    public async Task Mistakes_are_listed_with_exit_1_and_json_returns_them()
    {
        var path = File("definition.json", """{"formatVersion":5}""");

        var text = await TestCli.Run(["validate", "--file", path, .. Common], Answering(Mistake));
        var json = await TestCli.Run(["validate", "--file", path, "--json", .. Common], Answering(Mistake));

        Assert.Equal(1, text.ExitCode);
        Assert.Contains("Mistakes, 1:", text.Error, StringComparison.Ordinal);
        Assert.Contains(
            "declaration.formatVersion This definition is format 5 and the bot wants format 4. Update Suwagcms.Sdk on the CMS",
            text.Error,
            StringComparison.Ordinal
        );
        Assert.Equal(1, json.ExitCode);
        using var document = JsonDocument.Parse(json.Output);
        Assert.Equal("declaration.formatVersion", document.RootElement.GetProperty("mistakes")[0].GetProperty("code").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-15")]
    public async Task A_file_that_is_not_valid_exits_1_one_that_cannot_be_read_exits_2_and_nothing_is_sent()
    {
        var broken = File("broken.yaml", "a: [1");
        var server = Answering("");

        var invalid = await TestCli.Run(["validate", "--file", broken, .. Common], server);
        var missing = await TestCli.Run(["validate", "--file", Path.Combine(folder, "nope.yaml"), .. Common], server);
        var unknownStage = await TestCli.Run(["validate", "--file", broken, "--stage", "later", .. Common], server);
        var open = await TestCli.Run(["validate", "--file", broken, "--open", .. Common], server);

        Assert.Equal(1, invalid.ExitCode);
        Assert.Contains("is not valid YAML or JSON", invalid.Error, StringComparison.Ordinal);
        Assert.Equal(2, missing.ExitCode);
        Assert.Equal(2, unknownStage.ExitCode);
        Assert.Equal(2, open.ExitCode);
        Assert.Empty(server.Requests);
    }
}
