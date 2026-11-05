using System.Net;
using System.Text;
using System.Text.Json;
using Suwagcms.Cli.Tests.Plugin;

namespace Suwagcms.Cli.Tests.Usage;

public sealed class UsageCommandTests
{
    private static readonly string[] OnBot = ["--server", "http://cms.internal", "--bot", "rzeczoznawca"];

    private const string Used =
        """{"usedBy":[{"kind":"article","id":"rabaty","field":"llmGuide"},{"kind":"article","id":"wycena","field":"content"}],"uses":[{"kind":"article","id":"warunki","field":"content"}]}""";

    private const string Nothing = """{"usedBy":[],"uses":[]}""";

    private const string Refusal = """
        {"mistakes":[{"code":"reference.missing","stage":"Save","resource":{"kind":"Article","id":"cennik"},"path":"name",
        "message":"The article 'cennik' is used in 2 place(s), so it cannot be deleted.","fix":"Remove the reference to 'cennik' in each place listed in `related`, then delete the article again.",
        "related":[{"resource":{"kind":"Article","id":"rabaty"},"location":"llmGuide"},{"resource":{"kind":"Article","id":"wycena"},"location":"content"}]}]}
        """;

    private static string[] Args(params string[] words) => [.. words, .. OnBot];

    private static StubServer Recording(List<string> calls, HttpStatusCode status, string json) =>
        new(request =>
        {
            calls.Add($"{request.Method.Method} {request.RequestUri?.PathAndQuery}");
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        });

    [Fact]
    [Trait("criterion", "AC-delete-guard-3")]
    public async Task A_refused_delete_exits_1_and_lists_every_referrer_with_its_field_and_a_link()
    {
        var result = await TestCli.Run(
            Args("articles", "delete", "cennik"),
            StubServer.Answering(HttpStatusCode.UnprocessableEntity, Refusal)
        );

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("reference.missing", result.Error, StringComparison.Ordinal);
        Assert.Contains(
            "article rabaty (llmGuide)  http://cms.internal/bots/rzeczoznawca/articles?open=rabaty",
            result.Error,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "article wycena (content)  http://cms.internal/bots/rzeczoznawca/articles?open=wycena",
            result.Error,
            StringComparison.Ordinal
        );
        Assert.Contains("Remove the reference to 'cennik'", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-3")]
    public async Task A_refused_delete_with_json_gives_the_referrers_as_data()
    {
        var result = await TestCli.Run(
            [.. Args("articles", "delete", "cennik"), "--json"],
            StubServer.Answering(HttpStatusCode.UnprocessableEntity, Refusal)
        );

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.Output);
        var related = document.RootElement.GetProperty("mistakes")[0].GetProperty("related").EnumerateArray().ToList();
        Assert.Equal(
            ["rabaty:llmGuide", "wycena:content"],
            related.Select(item =>
                $"{item.GetProperty("resource").GetProperty("id").GetString()}:{item.GetProperty("location").GetString()}"
            )
        );
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-8")]
    public async Task Usage_prints_the_used_by_and_uses_lists_and_only_reads()
    {
        var calls = new List<string>();

        var result = await TestCli.Run(Args("usage", "article", "cennik"), Recording(calls, HttpStatusCode.OK, Used));

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["GET /api/bots/rzeczoznawca/articles/cennik/usage"], calls);
        Assert.Equal(
            ["Used by:  article rabaty (llmGuide), article wycena (content)", "Uses:     article warunki (content)"],
            result.Output.ReplaceLineEndings("\n").TrimEnd().Split('\n')
        );
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-8")]
    public async Task Usage_with_json_gives_the_same_two_lists_as_data()
    {
        var result = await TestCli.Run([.. Args("usage", "fact", "customer.city"), "--json"], Recording([], HttpStatusCode.OK, Used));

        using var document = JsonDocument.Parse(result.Output);
        Assert.Equal(
            ["rabaty:llmGuide", "wycena:content"],
            document
                .RootElement.GetProperty("usedBy")
                .EnumerateArray()
                .Select(link => $"{link.GetProperty("id").GetString()}:{link.GetProperty("field").GetString()}")
        );
        Assert.Equal("warunki", document.RootElement.GetProperty("uses")[0].GetProperty("id").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-7")]
    public async Task Usage_of_a_resource_nothing_relates_to_says_so()
    {
        var result = await TestCli.Run(Args("usage", "procedure", "orderValuation"), Recording([], HttpStatusCode.OK, Nothing));

        Assert.Equal(["Used by:  nothing", "Uses:     nothing"], result.Output.ReplaceLineEndings("\n").TrimEnd().Split('\n'));
    }

    [Fact]
    public async Task Usage_of_a_kind_it_does_not_know_is_a_wrong_use()
    {
        var result = await TestCli.Run(Args("usage", "kb", "offer"), Recording([], HttpStatusCode.OK, Nothing));

        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-11")]
    public void The_main_skill_has_a_line_for_suwagcms_usage()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", "suwagcms", "SKILL.md"));

        Assert.Contains("usage", CommandList.Listed(text));
    }
}
