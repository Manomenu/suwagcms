using System.Net;
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
        Assert.True((await facts.Add("customer.city", FactApi.Text())).IsSuccessStatusCode);
        Assert.True(
            (
                await facts.Add(
                    "result.value",
                    """{"type":"text","description":"The result of an action","maxLength":9,"writableBy":"assign"}"""
                )
            ).IsSuccessStatusCode
        );
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
        """{"instruction":"Tell the customer where to go.","articles":["officeContact"],"facts":["result.value","customer.city"]}""";

    [Fact]
    [Trait("criterion", "AC-inform-block-1")]
    [Trait("criterion", "AC-inform-block-4")]
    public async Task An_inform_reads_back_with_its_instruction_its_lists_in_order_and_its_next_naming_an_assign_fact()
    {
        var scene = await NewBot();

        Assert.Equal(HttpStatusCode.OK, (await scene.Procedures.Add("orderValuation", Informing(Filled))).StatusCode);
        var read = await ProcedureApi.DetailOf(await scene.Procedures.Get("orderValuation"));

        var inform = read.Steps["tell"].Inform;
        Assert.NotNull(inform);
        Assert.Equal("Tell the customer where to go.", inform.Instruction);
        Assert.Equal(["officeContact"], inform.Articles);
        Assert.Equal(["result.value", "customer.city"], inform.Facts);
        Assert.Equal("wait", read.Steps["tell"].Next?.Step);
        Assert.Empty(read.NotReady);
    }

    [Fact]
    [Trait("criterion", "AC-inform-block-2")]
    [Trait("criterion", "AC-inform-block-3")]
    [Trait("criterion", "AC-inform-block-5")]
    public async Task What_only_another_bot_has_is_refused_each_at_once_with_nothing_saved()
    {
        var scene = await NewBot();
        var other = await NewBot();
        Assert.True((await other.Articles.Add("elsewhere", ArticleApi.Body())).IsSuccessStatusCode);
        Assert.True((await other.Procedures.Facts.Add("other.fact", FactApi.Text())).IsSuccessStatusCode);

        var response = await scene.Procedures.Add(
            "orderValuation",
            Informing("""{"instruction":"Say it.","articles":["elsewhere"],"facts":["other.fact"]}""")
        );
        var mistakes = await FactApi.MistakesOf(response);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["reference.missing", "reference.missing"], mistakes.Select(mistake => mistake.GetProperty("code").GetString()));
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
    [InlineData("""{"instruction":"Say it."}""", new[] { "step.inform.empty" })]
    [InlineData("""{"facts":["customer.city"]}""", new[] { "instruction.blank" })]
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

        var field = await scene.Procedures.Add("orderValuation", Informing("""{"instruction":"Say it.","points":["turnaround"]}"""));
        var two = await scene.Procedures.Add(
            "orderValuation",
            """{"description":"d","firstStep":"a","steps":{"a":{"inform":{"instruction":"Say it."},"say":"Hi"}}}"""
        );

        var mistake = Assert.Single(await FactApi.MistakesOf(field));
        Assert.Equal("save.invalid", mistake.GetProperty("code").GetString());
        Assert.Contains("points", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("removed", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal);
        Assert.Equal(["step.block.multiple"], await ProcedureApi.CodesOf(two));
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-14")]
    [Trait("criterion", "AC-delete-guard-15")]
    public async Task A_fact_or_an_article_an_inform_names_cannot_be_deleted_and_can_once_the_inform_is_gone()
    {
        var scene = await NewBot();
        Assert.True((await scene.Procedures.Add("orderValuation", Informing(Filled))).IsSuccessStatusCode);

        var fact = await scene.Procedures.Facts.Delete("result.value");
        var article = await scene.Articles.Delete("officeContact");

        Assert.Equal(["reference.missing"], await FactApi.CodesOf(fact));
        Assert.Equal(["reference.missing"], await FactApi.CodesOf(article));
        var related = Assert.Single(Assert.Single(await FactApi.MistakesOf(article)).GetProperty("related").EnumerateArray());
        Assert.Equal("steps.tell.inform.articles[0]", related.GetProperty("location").GetString());

        Assert.True((await scene.Procedures.Delete("orderValuation")).IsSuccessStatusCode);
        Assert.True((await scene.Procedures.Facts.Delete("result.value")).IsSuccessStatusCode);
        Assert.True((await scene.Articles.Delete("officeContact")).IsSuccessStatusCode);
    }
}
