using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Procedures;

namespace Suwagcms.Server.Tests.Articles;

public sealed class ArticleProcedureLinksTests(TestDatabase database, ClassServer server) : IClassFixture<ClassServer>
{
    private const string Link = "[the valuation](cms://procedure/orderValuation)";

    private TestApp App => server.App;

    private sealed record Scene(ArticleApi Articles, ProcedureApi Procedures);

    private async Task<Scene> NewBot()
    {
        _ = database.DataSource;
        var client = App.CreateClient();
        var articles = await ArticleApi.NewBotAsync(client);
        var procedures = new ProcedureApi(client, new FactApi(client, articles.Bot));
        Assert.True((await procedures.Add("conversation", ProcedureApi.Conversation)).IsSuccessStatusCode);
        Assert.True((await procedures.Add("orderValuation", """{"description":"Orders a valuation."}""")).IsSuccessStatusCode);
        return new Scene(articles, procedures);
    }

    private static string Guided(string guide) =>
        JsonSerializer.Serialize(
            new
            {
                knowledgeBase = "offer",
                title = "T",
                content = "Text.",
                llmGuide = guide,
                description = "D",
            }
        );

    private static string Contented(string content) =>
        JsonSerializer.Serialize(
            new
            {
                knowledgeBase = "offer",
                title = "T",
                content,
                llmGuide = "G",
                description = "D",
            }
        );

    [Fact]
    [Trait("criterion", "AC-article-procedure-links-3")]
    public async Task A_link_in_content_is_refused_with_its_code_and_fix()
    {
        var scene = await NewBot();

        var refused = await scene.Articles.Add("wycena", Contented($"Start {Link}."));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal("link.procedure.notInGuide", mistake.GetProperty("code").GetString());
        Assert.Equal("Move the link to llmGuide.", mistake.GetProperty("fix").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-article-procedure-links-4")]
    public async Task A_procedure_only_another_bot_has_is_refused_naming_the_field_and_the_id()
    {
        var scene = await NewBot();
        var other = await NewBot();
        Assert.True((await other.Procedures.Add("elsewhere", """{"description":"D"}""")).IsSuccessStatusCode);

        var refused = await scene.Articles.Add("wycena", Guided("Start [it](cms://procedure/elsewhere)."));

        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal("reference.missing", mistake.GetProperty("code").GetString());
        Assert.Equal("llmGuide", mistake.GetProperty("at").GetString());
        Assert.Contains("procedure 'elsewhere'", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-article-procedure-links-7")]
    public async Task A_guide_link_is_saved_as_written_and_listed_among_what_the_article_uses()
    {
        var scene = await NewBot();

        Assert.Equal(HttpStatusCode.OK, (await scene.Articles.Add("wycena", Guided($"Start {Link}."))).StatusCode);

        var read = await (await scene.Articles.Get("wycena")).Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal($"Start {Link}.", read.GetProperty("llmGuide").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-18")]
    public async Task A_procedure_a_guide_links_to_cannot_be_deleted_and_can_once_the_link_is_gone()
    {
        var scene = await NewBot();
        Assert.True((await scene.Articles.Add("wycena", Guided($"Start {Link}."))).IsSuccessStatusCode);

        var refused = await scene.Procedures.Delete("orderValuation");

        Assert.Equal(["reference.missing"], await FactApi.CodesOf(refused));
        var related = Assert.Single(Assert.Single(await FactApi.MistakesOf(refused)).GetProperty("related").EnumerateArray());
        Assert.Equal("llmGuide", related.GetProperty("location").GetString());

        Assert.True((await scene.Articles.Delete("wycena")).IsSuccessStatusCode);
        Assert.True((await scene.Procedures.Delete("orderValuation")).IsSuccessStatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-loop-guard-15")]
    public async Task A_guide_link_that_closes_a_loop_through_the_knowledge_is_refused_with_the_path()
    {
        var scene = await NewBot();
        Assert.True((await scene.Articles.Add("wycena", Guided("No link yet."))).IsSuccessStatusCode);
        var body =
            """{"description":"Orders a valuation.","firstStep":"tell","steps":{"tell":{"inform":{"instruction":"Say so.","knowledge":[{"knowledgeBase":"offer","articles":["wycena"]}]}}}}""";
        Assert.True((await scene.Procedures.Put("orderValuation", body)).IsSuccessStatusCode);

        var refused = await scene.Articles.Put("wycena", Guided($"Start {Link}."));

        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal("reference.loop", mistake.GetProperty("code").GetString());
        Assert.Contains("orderValuation", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-27")]
    public async Task Changing_a_procedures_id_rewrites_each_guide_link_byte_for_byte()
    {
        var scene = await NewBot();
        var guide =
            $"Start {Link},\r\n`[x](cms://procedure/orderValuation)` and orderValuation stay, [a](cms://article/orderValuation) too.";
        Assert.True((await scene.Articles.Add("orderValuation", Guided("Own."))).IsSuccessStatusCode);
        Assert.True((await scene.Articles.Add("wycena", Guided(guide))).IsSuccessStatusCode);
        using var content = JsonContent.Create(new { newId = "valuation" });

        var renamed = await App.CreateClient()
            .PostAsync($"/api/bots/{scene.Articles.Bot}/procedures/orderValuation/rename", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        var read = await (await scene.Articles.Get("wycena")).Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(
            guide.Replace("(cms://procedure/orderValuation),", "(cms://procedure/valuation),", StringComparison.Ordinal),
            read.GetProperty("llmGuide").GetString()
        );
    }
}
