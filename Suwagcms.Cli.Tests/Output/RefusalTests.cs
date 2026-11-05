using System.Net;
using System.Text.Json;

namespace Suwagcms.Cli.Tests.Output;

public sealed class RefusalTests
{
    private const string TwoMistakes = """
        {"title":"Refused","status":422,"mistakes":[
          {"code":"fact.duplicate","path":"facts[1]","message":"Two facts share the name customer.city.","fix":"Rename one of them."},
          {"code":"reference.missing","path":"facts[2]","message":"No article has the id a-9.","fix":"Create the article or point at another."}]}
        """;

    // Why: no command changes anything yet, so a read stands in; the refusal is read by the code every command shares.
    private static StubServer Refusing() => StubServer.Answering(HttpStatusCode.UnprocessableEntity, TwoMistakes);

    [Fact]
    [Trait("criterion", "AC-cli-foundation-5")]
    public async Task A_refusal_prints_every_mistake_with_its_fix_and_exits_1()
    {
        var result = await TestCli.Run(["status", "--server", "http://cms.internal"], Refusing());

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("", result.Output);
        foreach (
            var expected in new[]
            {
                "fact.duplicate",
                "Two facts share",
                "Rename one of them.",
                "reference.missing",
                "No article has the id a-9.",
                "Create the article",
            }
        )
        {
            Assert.Contains(expected, result.Error, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-6")]
    public async Task A_refusal_with_json_prints_one_document_with_the_same_mistakes_and_their_codes_and_exits_1()
    {
        var result = await TestCli.Run(["status", "--json", "--server", "http://cms.internal"], Refusing());

        Assert.Equal(1, result.ExitCode);
        Assert.Equal("", result.Error);
        using var document = JsonDocument.Parse(result.Output);
        var mistakes = document.RootElement.GetProperty("mistakes").EnumerateArray().ToList();
        Assert.Equal(["fact.duplicate", "reference.missing"], mistakes.Select(mistake => mistake.GetProperty("code").GetString()));
        Assert.Equal(
            ["Rename one of them.", "Create the article or point at another."],
            mistakes.Select(mistake => mistake.GetProperty("fix").GetString())
        );
    }

    [Fact]
    public async Task A_client_error_without_mistakes_is_not_a_refusal()
    {
        var notFound = StubServer.Answering(HttpStatusCode.NotFound, """{"status":404}""");

        var result = await TestCli.Run(["status", "--server", "http://cms.internal"], notFound);

        Assert.Equal(3, result.ExitCode);
    }
}
