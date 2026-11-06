using System.Net;
using System.Text.Json;
using Suwagcms.Server.Tests.Articles;
using Suwagcms.Server.Tests.Facts;

namespace Suwagcms.Server.Tests.Procedures;

public sealed class InformBlockTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    private sealed record Scene(ArticleApi Articles, ProcedureApi Procedures);

    // Why: a bot with two bases and one article in the second, so "any knowledge base of this bot" is what is tried.
    private async Task<Scene> NewBot()
    {
        _ = database.Pool;
        var client = app.CreateClient();
        var articles = await ArticleApi.NewBotAsync(client);
        var facts = new FactApi(client, articles.Bot);
        Assert.True((await articles.Add("officeContact", ArticleApi.Body(knowledgeBase: "offer"))).IsSuccessStatusCode);
        var procedures = new ProcedureApi(client, facts);
        Assert.True((await procedures.Add("conversation", ProcedureApi.Conversation)).IsSuccessStatusCode);
        return new Scene(articles, procedures);
    }

    private static string Informing(string inform) =>
        """
            {"description":"Tells the customer.","firstStep":"tell","steps":{"tell":{"inform":INFORM,"next":"wait"},"wait":{"listen":{}}}}
            """.Replace("INFORM", inform, StringComparison.Ordinal);

    private const string Filled =
        """{"instruction":"Tell the customer what happens next.","knowledge":[{"knowledgeBase":"offer","articles":["officeContact"],"searchQueries":["delivery times"]}],"points":{"turnaround":"Ready within 7 working days.","delivery":"Sent by e-mail.","anotherLongerName":"A third point.","a":"The shortest name."}}""";

    [Fact]
    [Trait("criterion", "AC-inform-block-1")]
    public async Task An_inform_reads_back_with_its_instruction_its_knowledge_its_points_and_its_next()
    {
        var scene = await NewBot();

        Assert.Equal(HttpStatusCode.OK, (await scene.Procedures.Add("orderValuation", Informing(Filled))).StatusCode);
        var read = await ProcedureApi.DetailOf(await scene.Procedures.Get("orderValuation"));

        var inform = read.Steps["tell"].Inform;
        Assert.NotNull(inform);
        Assert.Equal("Tell the customer what happens next.", inform.Instruction);
        Assert.Equal(["officeContact"], inform.Knowledge?[0].Articles);
        Assert.Equal(["delivery times"], inform.Knowledge?[0].SearchQueries);
        Assert.Equal("Sent by e-mail.", inform.Points?["delivery"]);
        Assert.Equal("wait", read.Steps["tell"].Next?.Step);
        Assert.Empty(read.NotReady);
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-13")]
    public async Task The_points_come_back_from_the_store_and_in_a_version_in_the_order_written()
    {
        var scene = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await scene.Procedures.Add("orderValuation", Informing(Filled))).StatusCode);

        var response = await scene.Procedures.Get("orderValuation");
        var read = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;

        Assert.Equal(
            ["turnaround", "delivery", "anotherLongerName", "a"],
            read.GetProperty("steps")
                .GetProperty("tell")
                .GetProperty("inform")
                .GetProperty("points")
                .EnumerateObject()
                .Select(point => point.Name)
        );
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-2")]
    [Trait("criterion", "AC-inform-block-5")]
    public async Task What_only_another_bot_has_and_a_wrong_point_name_are_refused_each_at_once_with_nothing_saved()
    {
        var scene = await NewBot();
        var other = await NewBot();
        Assert.True((await other.Articles.Add("elsewhere", ArticleApi.Body())).IsSuccessStatusCode);

        var response = await scene.Procedures.Add(
            "orderValuation",
            Informing(
                """{"instruction":"Say it.","knowledge":[{"knowledgeBase":"offer","articles":["elsewhere"]}],"points":{"bad_name":"Text."}}"""
            )
        );
        var mistakes = await FactApi.MistakesOf(response);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["reference.missing", "step.inform.pointName"], mistakes.Select(mistake => mistake.GetProperty("code").GetString()));
        Assert.All(
            mistakes,
            mistake => Assert.Contains("step 'tell'", mistake.GetProperty("message").GetString(), StringComparison.Ordinal)
        );
        Assert.Equal(HttpStatusCode.NotFound, (await scene.Procedures.Get("orderValuation")).StatusCode);
    }

    [Theory]
    [Trait("criterion", "AC-inform-block-6")]
    [InlineData("""{"instruction":" "}""", new[] { "instruction.blank", "step.inform.empty" })]
    [InlineData("{}", new[] { "instruction.blank", "step.inform.empty" })]
    [InlineData("""{"instruction":"Say it.","points":{}}""", new[] { "step.inform.empty" })]
    [InlineData("""{"instruction":"Say it.","points":{"turnaround":" "}}""", new[] { "step.inform.pointBlank" })]
    [InlineData("""{"points":{"turnaround":"Text."}}""", new[] { "instruction.blank" })]
    public async Task An_unfinished_inform_is_saved_listed_and_creating_a_version_reports_it(string inform, string[] codes)
    {
        var scene = await NewBot();

        var saved = await ProcedureApi.DetailOf(await scene.Procedures.Add("orderValuation", Informing(inform)));
        using var empty = new StringContent("");
        var refused = await app.CreateClient()
            .PostAsync($"/api/bots/{scene.Articles.Bot}/versions", empty, TestContext.Current.CancellationToken);

        Assert.Equal(codes, ProcedureApi.NotReadyCodes(saved));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal(codes, await FactApi.CodesOf(refused));
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-7")]
    public async Task A_field_the_block_does_not_have_and_a_second_block_are_each_refused_naming_what_is_wrong()
    {
        var scene = await NewBot();

        var field = await scene.Procedures.Add("orderValuation", Informing("""{"instruction":"Say it.","facts":["customer.city"]}"""));
        var two = await scene.Procedures.Add(
            "orderValuation",
            """{"description":"d","firstStep":"a","steps":{"a":{"inform":{"instruction":"Say it."},"say":"Hi"}}}"""
        );

        var mistake = Assert.Single(await FactApi.MistakesOf(field));
        Assert.Equal("save.invalid", mistake.GetProperty("code").GetString());
        Assert.Contains("facts", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("removed", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal);
        Assert.Equal(["step.block.multiple"], await ProcedureApi.CodesOf(two));
    }
}
