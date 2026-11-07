using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Cli.Versions;
using Suwagcms.Contracts.Facts;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Cli.Tests.Versions;

public sealed class ExportTests : IDisposable
{
    private static readonly string[] OnBot = ["--server", "http://cms.internal", "--bot", "rzeczoznawca"];

    private readonly string scratch = Directory.CreateTempSubdirectory("suwagcms-export-tests").FullName;

    public void Dispose() => Directory.Delete(scratch, recursive: true);

    private string Out => Path.Combine(scratch, "v14");

    private static string File_(string path, string id, string kind, string data) =>
        $$"""{"kind":{{kind}},"id":"{{id}}","path":"{{path}}","data":{{data}}}""";

    private static string Answer(params string[] files) => $$"""{"bot":"rzeczoznawca","number":14,"files":[{{string.Join(",", files)}}]}""";

    private static readonly string Whole = Answer(
        File_("version.yaml", "version", "null", """{"bot":"rzeczoznawca","number":14,"formatVersion":3,"digest":"abc"}"""),
        File_(
            "facts/customer.city.yaml",
            "customer.city",
            "\"GlobalFact\"",
            """{"name":"customer.city","type":"text","description":"The city","maxLength":80}"""
        ),
        File_("knowledge-bases/oferta.yaml", "oferta", "\"KnowledgeBase\"", """{"id":"oferta","title":"Oferta","language":"pl"}"""),
        File_(
            "articles/cennik.yaml",
            "cennik",
            "\"Article\"",
            """{"id":"cennik","knowledgeBase":"oferta","title":"Cennik","content":"Dom: 900\nMieszkanie: 500\n","llmGuide":"G","description":"D"}"""
        ),
        File_(
            "procedures/conversation.yaml",
            "conversation",
            "\"Procedure\"",
            """{"id":"conversation","description":"Base","firstStep":"wait","steps":{"wait":{"listen":{}}}}"""
        )
    );

    private static StubServer Serving(string json) =>
        new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

    private static Dictionary<string, byte[]> Folder(string root) =>
        Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(root, file).Replace('\\', '/'), File.ReadAllBytes);

    [Fact]
    [Trait("criterion", "AC-version-export-1")]
    public async Task It_writes_version_yaml_and_one_file_per_resource_named_by_its_id_in_a_folder_per_kind()
    {
        var server = Serving(Whole);

        var result = await TestCli.Run(["export", "14", "--out", Out, .. OnBot], server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://cms.internal/api/bots/rzeczoznawca/versions/14/export", Assert.Single(server.Requests).ToString());
        Assert.Equal(
            [
                "articles/cennik.yaml",
                "facts/customer.city.yaml",
                "knowledge-bases/oferta.yaml",
                "procedures/conversation.yaml",
                "version.yaml",
            ],
            Folder(Out).Keys.Order(StringComparer.Ordinal)
        );
        Assert.Contains(
            $"Exported v14 of rzeczoznawca to {Out}: 1 facts, 1 knowledge bases, 1 articles, 1 procedures",
            result.Output,
            StringComparison.Ordinal
        );
    }

    [Theory]
    [InlineData("one line")]
    [InlineData("two\nlines")]
    [InlineData("ends with a line break\n")]
    [InlineData("ends with two\n\n")]
    [InlineData("\nstarts with a line break")]
    [InlineData("  leading spaces\n  and more\n")]
    [InlineData("trailing spaces  \nand  \n")]
    [InlineData("a\r\nwindows\r\ntext\r\n")]
    [InlineData("a lone\rreturn")]
    [InlineData("\ttabs\n\tinside\t\n")]
    [InlineData("  \n  ")]
    [InlineData(" ")]
    [InlineData("# not a comment\nkey: value\n- item\n")]
    [InlineData("See [Cennik](cms://article/cennik)\n`[x](cms://article/y)`\n")]
    [InlineData("Żółć: łódź — 900 zł\n🎉\n")]
    [InlineData("line\u0085next\u2028other\u2029end\n")]
    [InlineData("---\n...\n%YAML 1.1\n")]
    [InlineData("'single' \"double\" \\back\n{a: [b]}\n")]
    [InlineData("null")]
    [InlineData("~")]
    [InlineData("true")]
    [InlineData("123")]
    [InlineData("1.5")]
    [InlineData("2024-01-01")]
    [InlineData("0x10")]
    [InlineData("a: b")]
    [InlineData("- a")]
    [InlineData("")]
    [Trait("criterion", "AC-version-export-2")]
    public void Reading_a_file_back_gives_the_text_byte_for_byte(string text)
    {
        var data = new JsonObject
        {
            ["id"] = "cennik",
            ["content"] = text,
            ["llmGuide"] = text,
            ["values"] = new JsonArray(text, "1"),
            ["maxLength"] = 80,
            ["steps"] = new JsonObject { ["wait"] = new JsonObject { ["listen"] = new JsonObject() } },
        };
        var file = new ExportedFile(ResourceKind.Article, "cennik", "articles/cennik.yaml", JsonSerializer.SerializeToElement(data));

        var written = ExportYaml.Text(new VersionExport("rzeczoznawca", 14, [file]), file);

        Assert.True(JsonNode.DeepEquals(data, ExportedYamlReader.Read(written)), written);
    }

    [Fact]
    [Trait("criterion", "AC-version-export-2")]
    public void A_text_of_several_lines_is_written_line_by_line_so_a_line_can_be_found_with_grep()
    {
        var data = JsonSerializer.SerializeToElement(new { content = "Dom: 900 zł\nMieszkanie: 500 zł\n" });
        var file = new ExportedFile(ResourceKind.Article, "cennik", "articles/cennik.yaml", data);

        var lines = ExportYaml.Text(new VersionExport("rzeczoznawca", 14, [file]), file).Split('\n');

        Assert.Contains("  Dom: 900 zł", lines);
        Assert.Contains("  Mieszkanie: 500 zł", lines);
    }

    [Fact]
    [Trait("criterion", "AC-version-export-3")]
    public async Task Exporting_twice_gives_byte_identical_folders()
    {
        var other = Path.Combine(scratch, "again");

        await TestCli.Run(["export", "14", "--out", Out, .. OnBot], Serving(Whole));
        await TestCli.Run(["export", "14", "--out", other, .. OnBot], Serving(Whole));

        var first = Folder(Out);
        var second = Folder(other);
        Assert.Equal(first.Keys, second.Keys);
        Assert.All(first, file => Assert.Equal(file.Value, second[file.Key]));
        Assert.All(first.Values, bytes => Assert.DoesNotContain((byte)'\r', bytes));
    }

    [Theory]
    [InlineData("facts/customer.city.yaml", "suwagcms facts edit customer.city")]
    [InlineData("knowledge-bases/oferta.yaml", "suwagcms kb edit oferta")]
    [InlineData("articles/cennik.yaml", "suwagcms articles edit cennik")]
    [InlineData("procedures/conversation.yaml", "suwagcms procedures edit conversation --file <file>")]
    [Trait("criterion", "AC-version-export-4")]
    public async Task Every_resource_file_starts_with_a_comment_naming_the_version_saying_changes_do_nothing_and_the_command_that_changes_it(
        string path,
        string command
    )
    {
        await TestCli.Run(["export", "14", "--out", Out, .. OnBot], Serving(Whole));

        var lines = (await File.ReadAllTextAsync(Path.Combine(Out, path), TestContext.Current.CancellationToken)).Split('\n');

        Assert.Equal(
            "# Read-only export of v14 of bot rzeczoznawca. Changes here do nothing, and no command reads this file back.",
            lines[0]
        );
        Assert.Equal($"# To change this: {command}", lines[1]);
    }

    [Fact]
    [Trait("criterion", "AC-version-export-4")]
    public async Task Version_yaml_holds_the_bot_the_number_the_format_version_and_the_digest()
    {
        await TestCli.Run(["export", "14", "--out", Out, .. OnBot], Serving(Whole));

        var read = ExportedYamlReader.Read(
            await File.ReadAllTextAsync(Path.Combine(Out, "version.yaml"), TestContext.Current.CancellationToken)
        );

        Assert.Equal("rzeczoznawca", read?["bot"]?.GetValue<string>());
        Assert.Equal(14, read?["number"]?.GetValue<long>());
        Assert.Equal(3, read?["formatVersion"]?.GetValue<long>());
        Assert.Equal("abc", read?["digest"]?.GetValue<string>());
    }

    [Fact]
    [Trait("criterion", "AC-version-export-6")]
    public async Task Without_a_number_the_latest_is_exported()
    {
        var calls = new List<string>();
        var server = new StubServer(request =>
        {
            calls.Add(request.RequestUri?.AbsolutePath ?? "");
            var json =
                calls.Count == 1
                    ? """[{"number":13,"createdAt":"2026-11-02T09:12:00+00:00","facts":0,"knowledgeBases":0,"articles":0,"procedures":0,"sha256":"a"},{"number":14,"createdAt":"2026-11-04T09:12:00+00:00","facts":0,"knowledgeBases":0,"articles":0,"procedures":0,"sha256":"b"}]"""
                    : Whole;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        });

        var result = await TestCli.Run(["export", "--out", Out, .. OnBot], server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["/api/bots/rzeczoznawca/versions", "/api/bots/rzeczoznawca/versions/14/export"], calls);
    }

    [Fact]
    [Trait("criterion", "AC-version-export-6")]
    public async Task A_bot_with_no_version_is_refused_with_exit_1_and_a_missing_out_exits_2()
    {
        var none = await TestCli.Run(["export", "--out", Out, .. OnBot], Serving("[]"));
        var noOut = await TestCli.Run(["export", "14", .. OnBot], Serving(Whole));

        Assert.Equal(1, none.ExitCode);
        Assert.Contains("has no version yet", none.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Out));
        Assert.Equal(2, noOut.ExitCode);
        Assert.Contains("--out is required", noOut.Error, StringComparison.Ordinal);
        Assert.Contains("Usage:", noOut.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-export-7")]
    public async Task A_folder_that_does_not_exist_is_created_and_an_empty_one_is_used()
    {
        var deep = Path.Combine(scratch, "a", "b", "new");
        var empty = Directory.CreateDirectory(Path.Combine(scratch, "empty")).FullName;

        Assert.Equal(0, (await TestCli.Run(["export", "14", "--out", deep, .. OnBot], Serving(Whole))).ExitCode);
        Assert.Equal(0, (await TestCli.Run(["export", "14", "--out", empty, .. OnBot], Serving(Whole))).ExitCode);

        Assert.True(File.Exists(Path.Combine(deep, "version.yaml")));
        Assert.True(File.Exists(Path.Combine(empty, "version.yaml")));
    }

    [Fact]
    [Trait("criterion", "AC-version-export-7")]
    public async Task A_folder_with_anything_in_it_is_refused_and_left_untouched()
    {
        var used = Directory.CreateDirectory(Path.Combine(scratch, "v12")).FullName;
        var kept = Path.Combine(used, "notes.txt");
        await File.WriteAllTextAsync(kept, "mine", TestContext.Current.CancellationToken);
        var server = Serving(Whole);

        var result = await TestCli.Run(["export", "14", "--out", used, .. OnBot], server);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains($"Not exported: {used} is not empty.", result.Error, StringComparison.Ordinal);
        Assert.Equal(["notes.txt"], Folder(used).Keys);
        Assert.Equal("mine", await File.ReadAllTextAsync(kept, TestContext.Current.CancellationToken));
        Assert.Empty(server.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-version-export-7")]
    public async Task A_folder_that_cannot_be_written_is_refused_with_its_reason()
    {
        var file = Path.Combine(scratch, "a-file");
        await File.WriteAllTextAsync(file, "", TestContext.Current.CancellationToken);

        var result = await TestCli.Run(["export", "14", "--out", Path.Combine(file, "under"), .. OnBot], Serving(Whole));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("cannot be written", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../../evil")]
    [InlineData("../evil")]
    [InlineData("/etc/evil")]
    [Trait("criterion", "AC-version-export-8")]
    public async Task An_id_that_would_write_outside_the_folder_is_refused_naming_it_and_nothing_is_written(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        var answer = Answer(
            File_("version.yaml", "version", "null", """{"bot":"rzeczoznawca"}"""),
            File_(id.StartsWith('/') ? $"{id}.yaml" : $"articles/{id}.yaml", id, "\"Article\"", """{"id":"x"}""")
        );

        var result = await TestCli.Run(["export", "14", "--out", Out, .. OnBot], Serving(answer));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains($"the id '{id}' would write outside", result.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Out));
    }

    [Fact]
    [Trait("criterion", "AC-version-export-10")]
    public async Task With_json_it_returns_the_folder_the_version_and_the_count_of_files_per_kind()
    {
        var result = await TestCli.Run(["export", "14", "--out", Out, "--json", .. OnBot], Serving(Whole));

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Output);
        var root = document.RootElement;
        Assert.Equal(Out, root.GetProperty("folder").GetString());
        Assert.Equal(14, root.GetProperty("version").GetInt32());
        Assert.Equal(1, root.GetProperty("files").GetProperty("facts").GetInt32());
        Assert.Equal(1, root.GetProperty("files").GetProperty("knowledgeBases").GetInt32());
        Assert.Equal(1, root.GetProperty("files").GetProperty("articles").GetInt32());
        Assert.Equal(1, root.GetProperty("files").GetProperty("procedures").GetInt32());
    }

    [Fact]
    [Trait("criterion", "AC-version-export-10")]
    public async Task Open_opens_the_versions_page_and_needs_no_folder()
    {
        Uri? opened = null;

        var result = await TestCli.Run(["export", "14", "--open", .. OnBot], openBrowser: page => opened = page);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://cms.internal/bots/rzeczoznawca/versions?open=14", opened?.ToString());
    }
}
