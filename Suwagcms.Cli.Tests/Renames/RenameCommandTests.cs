using System.Net;
using System.Text;
using System.Text.Json;

namespace Suwagcms.Cli.Tests.Renames;

public sealed class RenameCommandTests
{
    private static readonly string[] OnBot = ["--server", "http://cms.internal", "--bot", "rzeczoznawca"];

    private const string Answer = """
        {"kind":"article","oldId":"cennik","id":"priceList","revision":4,
         "referrers":[{"kind":"article","id":"rabaty","field":"llmGuide","links":2,"revision":6},
                      {"kind":"article","id":"wycena","field":"content","links":1,"revision":4}]}
        """;

    private static string[] Args(params string[] words) => [.. words, .. OnBot];

    private static StubServer Answering(HttpStatusCode status, string json, List<(string Url, string Body)>? calls = null) =>
        new(request =>
        {
            using var reader = new StreamReader(request.Content?.ReadAsStream() ?? Stream.Null);
            calls?.Add((request.RequestUri?.PathAndQuery ?? "", reader.ReadToEnd()));
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        });

    [Theory]
    [InlineData("article", "articles")]
    [InlineData("kb", "knowledge-bases")]
    [InlineData("fact", "facts")]
    [InlineData("procedure", "procedures")]
    [Trait("criterion", "AC-resource-rename-1")]
    public async Task The_command_asks_the_route_of_the_kind_with_the_new_id_and_says_what_was_done(string word, string collection)
    {
        var calls = new List<(string Url, string Body)>();

        var result = await TestCli.Run(Args("rename", word, "cennik", "priceList"), Answering(HttpStatusCode.OK, Answer, calls));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal($"/api/bots/rzeczoznawca/{collection}/cennik/rename", Assert.Single(calls).Url);
        Assert.Equal("priceList", JsonDocument.Parse(calls[0].Body).RootElement.GetProperty("newId").GetString());
        Assert.Contains("Renamed article 'cennik' to 'priceList'. Rewrote 2 referrers.", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-8")]
    public async Task A_dry_run_lists_each_referrer_and_field_and_says_nothing_was_changed()
    {
        var calls = new List<(string Url, string Body)>();

        var result = await TestCli.Run(
            Args("rename", "article", "cennik", "priceList", "--dry-run"),
            Answering(HttpStatusCode.OK, Answer, calls)
        );

        Assert.Equal(0, result.ExitCode);
        Assert.EndsWith("?dryRun=true", Assert.Single(calls).Url, StringComparison.Ordinal);
        Assert.Equal(
            [
                "Would rename article 'cennik' to 'priceList' and rewrite 2 referrers:",
                "  article rabaty (llmGuide, 2 links)",
                "  article wycena (content, 1 link)",
                "Nothing was changed.",
            ],
            result.Output.ReplaceLineEndings("\n").TrimEnd().Split('\n')
        );
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-9")]
    public async Task With_json_the_renamed_resource_and_the_referrers_are_data()
    {
        var result = await TestCli.Run(
            [.. Args("rename", "article", "cennik", "priceList"), "--json"],
            Answering(HttpStatusCode.OK, Answer)
        );

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.Output);
        Assert.Equal("priceList", document.RootElement.GetProperty("id").GetString());
        Assert.Equal("cennik", document.RootElement.GetProperty("oldId").GetString());
        var referrers = document.RootElement.GetProperty("referrers");
        Assert.Equal(2, referrers.GetArrayLength());
        Assert.Equal("llmGuide", referrers[0].GetProperty("field").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-9")]
    public async Task A_refusal_exits_1_and_with_json_gives_the_mistakes()
    {
        var refusal =
            """{"mistakes":[{"code":"resource.duplicateId","path":"id","message":"This bot already has an article 'priceList'.","fix":"Choose another id, or delete that article first."}]}""";

        var text = await TestCli.Run(
            Args("rename", "article", "cennik", "priceList"),
            Answering(HttpStatusCode.UnprocessableEntity, refusal)
        );
        var json = await TestCli.Run(
            [.. Args("rename", "article", "cennik", "priceList"), "--json"],
            Answering(HttpStatusCode.UnprocessableEntity, refusal)
        );

        Assert.Equal(1, text.ExitCode);
        Assert.Contains("already has an article 'priceList'", text.Error, StringComparison.Ordinal);
        Assert.Equal(1, json.ExitCode);
        using var document = JsonDocument.Parse(json.Output);
        Assert.Equal("resource.duplicateId", document.RootElement.GetProperty("mistakes")[0].GetProperty("code").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-12")]
    public async Task If_revision_is_sent_as_given()
    {
        var calls = new List<(string Url, string Body)>();

        await TestCli.Run(
            Args("rename", "article", "cennik", "priceList", "--if-revision", "3"),
            Answering(HttpStatusCode.OK, Answer, calls)
        );

        Assert.EndsWith("/rename?ifRevision=3", Assert.Single(calls).Url, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("article", "http://cms.internal/bots/rzeczoznawca/articles?open=priceList")]
    [InlineData("fact", "http://cms.internal/bots/rzeczoznawca/facts?open=priceList")]
    [InlineData("procedure", "http://cms.internal/bots/rzeczoznawca/procedures/priceList")]
    [InlineData("kb", "http://cms.internal/bots/rzeczoznawca/knowledge-bases")]
    [Trait("criterion", "AC-resource-rename-16")]
    public async Task Open_opens_the_page_of_the_resource_at_its_new_address(string word, string page)
    {
        var opened = new List<Uri>();

        var result = await TestCli.Run(
            [.. Args("rename", word, "cennik", "priceList"), "--open"],
            Answering(HttpStatusCode.OK, Answer),
            openBrowser: opened.Add
        );

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(page, result.Output, StringComparison.Ordinal);
        Assert.Equal(page, Assert.Single(opened).ToString());
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-17")]
    public void The_main_skill_has_a_line_for_rename()
    {
        var skill = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", "suwagcms", "SKILL.md"));

        Assert.Contains("- `suwagcms rename <kind> <id> <newId>", skill, StringComparison.Ordinal);
    }
}
