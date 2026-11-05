using System.Net;
using System.Text.Json;

namespace Suwagcms.Cli.Tests.Articles;

public sealed class ArticleLoopRefusalTests
{
    private static readonly string[] Edit =
    [
        "articles",
        "edit",
        "pricing",
        "--content",
        "See [d](cms://article/discounts).",
        "--server",
        "http://cms.internal",
        "--bot",
        "rzeczoznawca",
    ];

    private const string Refusal = """
        {"mistakes":[{"code":"reference.loop","path":"content","message":"These links close a loop: discounts → pricing → discounts.","fix":"Remove the link from 'discounts' to 'pricing', or another link on the path.","resource":{"kind":"Article","id":"discounts"},"related":[{"resource":{"kind":"Article","id":"pricing"},"location":"content"}]}]}
        """;

    [Fact]
    [Trait("criterion", "AC-loop-guard-1")]
    public async Task A_refused_loop_is_printed_with_its_code_and_path_and_exits_1()
    {
        var result = await TestCli.Run(Edit, StubServer.Answering(HttpStatusCode.UnprocessableEntity, Refusal));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("reference.loop", result.Error, StringComparison.Ordinal);
        Assert.Contains("discounts → pricing → discounts", result.Error, StringComparison.Ordinal);
        Assert.Contains("Remove the link", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-loop-guard-2")]
    public async Task With_json_the_path_is_given_as_data_in_the_related_list()
    {
        var result = await TestCli.Run([.. Edit, "--json"], StubServer.Answering(HttpStatusCode.UnprocessableEntity, Refusal));

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.Output);
        var mistake = document.RootElement.GetProperty("mistakes")[0];
        Assert.Equal("reference.loop", mistake.GetProperty("code").GetString());
        Assert.Equal("pricing", mistake.GetProperty("related")[0].GetProperty("resource").GetProperty("id").GetString());
    }
}
