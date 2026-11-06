using System.Net;
using System.Text;
using System.Text.Json;

namespace Suwagcms.Cli.Tests.Facts;

public sealed class EditGuardTests
{
    private const string Kind =
        """{"name":"order.kind","type":"enum","description":"What","writableBy":"correct","values":["house","flat"],"revision":1,"usedBy":["orderValuation"]}""";

    private const string Refusal = """
        {"mistakes":[{"code":"fact.enum.valueInUse","stage":"Save","resource":{"kind":"GlobalFact","id":"order.kind"},"path":"values",
          "message":"Value 'flat' of 'order.kind' is used, so it cannot be removed. Used by: procedure orderValuation, step ask (condition).",
          "fix":"Change these steps first, or keep the value.",
          "related":[{"resource":{"kind":"Procedure","id":"orderValuation"},"location":"steps.ask.next[0].when.order.kind"},
                     {"resource":{"kind":"Procedure","id":"leadIntake"},"location":"steps.tagOrder.assign.order.kind"}]}]}
        """;

    private static readonly string[] Edit =
    [
        "facts",
        "edit",
        "order.kind",
        "--values",
        "house",
        "land",
        "--server",
        "http://cms.internal",
        "--bot",
        "rzeczoznawca",
    ];

    private static StubServer Refusing() =>
        new(request =>
            request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Kind, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
                {
                    Content = new StringContent(Refusal, Encoding.UTF8, "application/problem+json"),
                }
        );

    [Fact]
    [Trait("criterion", "AC-edit-guard-10")]
    [Trait("criterion", "AC-edit-guard-9")]
    public async Task A_refused_edit_exits_1_and_prints_each_referrer_with_its_procedure_step_block_and_a_link()
    {
        var result = await TestCli.Run(Edit, Refusing());

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(
            "procedure orderValuation, step ask (condition)  http://cms.internal/bots/rzeczoznawca/procedures/orderValuation",
            result.Error,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "procedure leadIntake, step tagOrder (assign)  http://cms.internal/bots/rzeczoznawca/procedures/leadIntake",
            result.Error,
            StringComparison.Ordinal
        );
        Assert.Contains("Used by:", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("criterion", "AC-edit-guard-9")]
    [Trait("criterion", "AC-edit-guard-18")]
    [InlineData("reference.missing", "Used by:")]
    [InlineData("fact.enum.valueInUse", "Used by:")]
    [InlineData("fact.type.inUse", "Used by:")]
    [InlineData("fact.range.inUse", "Used by:")]
    [InlineData("fact.pattern.inUse", "Used by:")]
    [InlineData("fact.writableBy.inUse", "Used by:")]
    [InlineData("action.field.inUse", "Used by:")]
    [InlineData("reference.loop", "Related:")]
    public async Task A_refusal_heads_its_related_list_by_what_the_code_means(string code, string heading)
    {
        var refusal = Refusal.Replace("fact.enum.valueInUse", code, StringComparison.Ordinal);
        var server = new StubServer(request =>
            request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Kind, Encoding.UTF8, "application/json") }
                : new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
                {
                    Content = new StringContent(refusal, Encoding.UTF8, "application/problem+json"),
                }
        );

        var result = await TestCli.Run(Edit, server);

        Assert.Contains($"  {heading}{Environment.NewLine}", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-edit-guard-10")]
    public async Task With_json_the_referrers_are_data()
    {
        var result = await TestCli.Run([.. Edit, "--json"], Refusing());

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.Output);
        var related = document.RootElement.GetProperty("mistakes")[0].GetProperty("related");
        Assert.Equal(2, related.GetArrayLength());
        Assert.Equal("orderValuation", related[0].GetProperty("resource").GetProperty("id").GetString());
        Assert.Equal("steps.ask.next[0].when.order.kind", related[0].GetProperty("location").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-edit-guard-12")]
    public async Task A_refused_dry_run_gives_the_same_refusal_and_asks_the_server_for_a_dry_run()
    {
        var server = Refusing();

        var result = await TestCli.Run([.. Edit, "--dry-run"], server);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("fact.enum.valueInUse", result.Error, StringComparison.Ordinal);
        Assert.Contains("Change these steps first, or keep the value.", result.Error, StringComparison.Ordinal);
        Assert.Contains("dryRun=true", server.Requests.Last().Query, StringComparison.Ordinal);
    }
}
