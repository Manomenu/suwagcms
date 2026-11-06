using System.Net;
using System.Text;
using System.Text.Json;
using Suwagcms.Cli.Personas;
using Suwagcms.Cli.Procedures;
using Suwagcms.Cli.Versions;
using Suwagcms.Contracts.Facts;
using Suwagcms.Contracts.Personas;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.Format.Personas;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Cli.Tests.Personas;

public sealed class PersonaTests : IDisposable
{
    private static readonly string[] OnBot = ["--server", "http://cms.internal", "--bot", "rzeczoznawca"];

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private const string Empty = """{"personality":null,"scope":null,"revision":0}""";

    private readonly string folder = Directory.CreateTempSubdirectory("suwagcms-persona-cli").FullName;

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private sealed class Recorder
    {
        public List<(string Method, string Url, string Body)> Calls { get; } = [];

        public StubServer Serving(string read, string? saved = null) =>
            new(request =>
            {
                using var reader = new StreamReader(request.Content?.ReadAsStream() ?? Stream.Null);
                Calls.Add((request.Method.Method, request.RequestUri?.PathAndQuery ?? "", reader.ReadToEnd()));
                var json = request.Method == HttpMethod.Get ? read : saved ?? read;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            });
    }

    private static string[] Args(params string[] words) => [.. words, .. OnBot];

    private string FileWith(string name, string text)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-2")]
    public async Task Show_says_a_persona_never_saved_is_empty_and_how_to_write_one()
    {
        var result = await TestCli.Run(Args("persona", "show"), new Recorder().Serving(Empty));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("empty persona", result.Output, StringComparison.Ordinal);
        Assert.Contains("suwagcms persona edit --file", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-3")]
    public async Task Show_prints_each_text_of_several_lines_as_a_block()
    {
        var result = await TestCli.Run(
            Args("persona", "show"),
            new Recorder().Serving("""{"personality":"One\nTwo\n","scope":{"whenOutOfScope":"Decline\nHelp"},"revision":4}""")
        );

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            "personality: |\n  One\n  Two\nscope:\n  whenOutOfScope: |-\n    Decline\n    Help\nrevision: 4\n",
            result.Output.ReplaceLineEndings("\n")
        );
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-3")]
    public async Task Edit_sends_the_whole_of_a_yaml_file_with_its_line_breaks_and_guards_the_revision_it_read()
    {
        var recorder = new Recorder();
        var file = FileWith("persona.yaml", "personality: |\n  One\n  Two\nscope:\n  inScope: |-\n    In\n    scope\n");

        var result = await TestCli.Run(
            Args("persona", "edit", "--file", file),
            recorder.Serving(
                """{"personality":"Old","scope":null,"revision":3}""",
                """{"personality":"One\nTwo\n","scope":null,"revision":4}"""
            )
        );

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("revision 4", result.Output, StringComparison.Ordinal);
        var (method, url, body) = recorder.Calls[^1];
        Assert.Equal(("PUT", "/api/bots/rzeczoznawca/persona?ifRevision=3"), (method, url));
        Assert.Equal("""{"personality":"One\nTwo\n","scope":{"inScope":"In\nscope"}}""", body);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-3")]
    public async Task Edit_takes_a_json_file_and_the_revision_in_it_and_if_revision_and_dry_run_win()
    {
        var recorder = new Recorder();
        var file = FileWith("persona.json", """{"personality":"One\nTwo","revision":7}""");
        var server = recorder.Serving(Empty);

        var fromFile = await TestCli.Run(Args("persona", "edit", "--file", file), server);
        var given = await TestCli.Run(Args("persona", "edit", "--file", file, "--if-revision", "9", "--dry-run"), server);

        Assert.Equal((0, 0), (fromFile.ExitCode, given.ExitCode));
        var puts = recorder.Calls.Where(call => call.Method == "PUT").ToList();
        Assert.Equal("/api/bots/rzeczoznawca/persona?ifRevision=7", puts[0].Url);
        Assert.Equal("""{"personality":"One\nTwo"}""", puts[0].Body);
        Assert.Equal("/api/bots/rzeczoznawca/persona?ifRevision=9&dryRun=true", puts[1].Url);
        Assert.Contains("Nothing was changed", given.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-3")]
    public async Task A_file_that_cannot_be_read_or_holds_no_mapping_is_a_misuse_and_nothing_is_sent()
    {
        var recorder = new Recorder();
        var server = recorder.Serving(Empty);

        var missing = await TestCli.Run(Args("persona", "edit", "--file", Path.Combine(folder, "nowhere.yaml")), server);
        var list = await TestCli.Run(Args("persona", "edit", "--file", FileWith("list.yaml", "- one\n- two\n")), server);

        Assert.Equal((2, 2), (missing.ExitCode, list.ExitCode));
        Assert.Empty(recorder.Calls);
    }

    [Theory]
    [Trait("criterion", "AC-bot-persona-3")]
    [InlineData("One line")]
    [InlineData("One\nTwo")]
    [InlineData("One\nTwo\n")]
    [InlineData("One\n\n\n")]
    [InlineData("\n\nStarts with breaks")]
    [InlineData("  Starts with spaces\nthen not")]
    [InlineData("Not\n    then indented\nback")]
    [InlineData("Trailing spaces  \nand tab\t\n")]
    [InlineData("\tStarts with a tab\nsecond")]
    [InlineData("Blank-looking\n   \nline")]
    [InlineData("key: value\n- item\n# not a comment\n")]
    [InlineData("null")]
    [InlineData("~")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("\"quoted\"\n'and' {braces}: [x]")]
    [InlineData("Windows\r\nbreaks\r\n")]
    [InlineData("Zażółć gęślą jaźń 😀\nĘ")]
    [InlineData("---\nlooks like a document\n...\n")]
    [InlineData("| looks like a block\n> and a fold")]
    public void What_show_prints_reads_back_with_every_text_unchanged(string text)
    {
        var persona = new PersonaView(
            text,
            new PersonaScope
            {
                InScope = text,
                OutOfScope = "Between",
                WhenOutOfScope = text,
            },
            5
        );

        var (document, problem) = ProcedureFile.Parse(PersonaText.Shown(persona, "rzeczoznawca") + Environment.NewLine, "shown");

        Assert.Equal("", problem);
        Assert.NotNull(document);
        var read = document.Deserialize<PersonaView>(Web);
        Assert.Equal(persona, read);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-9")]
    public void Diff_names_the_persona_and_the_lines_added_and_removed_in_each_text()
    {
        var comparison = new VersionComparison(
            "rzeczoznawca",
            14,
            15,
            0,
            0,
            1,
            [],
            [
                new ResourceDifference(
                    ResourceKind.Persona,
                    "persona",
                    Difference.Changed,
                    [
                        new FieldDifference(
                            "scope.whenOutOfScope",
                            null,
                            null,
                            [new TextLine(LineChange.Removed, "Old way"), new TextLine(LineChange.Added, "New way")]
                        ),
                    ],
                    []
                ),
            ]
        );

        var lines = DiffText.Of(comparison).ReplaceLineEndings("\n").Split('\n');

        Assert.Contains("  persona persona changed", lines);
        Assert.Contains("    scope.whenOutOfScope", lines);
        Assert.Contains("      - Old way", lines);
        Assert.Contains("      + New way", lines);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-10")]
    public async Task History_of_the_persona_takes_no_id_and_asks_for_its_one_resource()
    {
        var recorder = new Recorder();

        var result = await TestCli.Run(
            Args("history", "persona"),
            recorder.Serving("""{"entries":[],"since":"2026-11-05T00:00:00+00:00","next":null}""")
        );

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("/api/bots/rzeczoznawca/persona/persona/history", Assert.Single(recorder.Calls).Url);
    }

    [Fact]
    [Trait("criterion", "AC-bot-persona-7")]
    public void An_exported_persona_tells_how_to_change_it_and_keeps_its_line_breaks_in_a_block()
    {
        using var data = JsonDocument.Parse("""{"personality":"One\nTwo\n"}""");
        var file = new ExportedFile(ResourceKind.Persona, "persona", "persona.yaml", data.RootElement);

        var text = ExportYaml.Text(new VersionExport("rzeczoznawca", 15, [file]), file);

        Assert.Contains("# To change this: suwagcms persona edit --file <file>", text, StringComparison.Ordinal);
        Assert.Contains("personality: |\n  One\n  Two\n", text, StringComparison.Ordinal);
    }
}
