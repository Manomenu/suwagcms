using System.Net;
using System.Net.Sockets;
using Suwagcms.Server.Tests.Articles;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.Procedures;

public sealed class KnowledgeTests(TestDatabase database, SharedApp shared) : IClassFixture<SharedApp>
{
    private TestApp App => shared.App;

    private sealed record Scene(ArticleApi Articles, ProcedureApi Procedures);

    private async Task<Scene> NewBot(bool withArticle = true)
    {
        _ = database.Pool;
        var client = App.CreateClient();
        var articles = await ArticleApi.NewBotAsync(client);
        if (withArticle)
        {
            Assert.True((await articles.Add("officeContact", ArticleApi.Body(knowledgeBase: "offer"))).IsSuccessStatusCode);
        }
        var procedures = new ProcedureApi(client, new FactApi(client, articles.Bot));
        Assert.True((await procedures.Add("conversation", ProcedureApi.Conversation)).IsSuccessStatusCode);
        return new Scene(articles, procedures);
    }

    private static string Informing(string knowledge) =>
        """
            {"description":"Tells.","firstStep":"tell","steps":{"tell":{"inform":
              {"instruction":"Say how to reach the office.","points":{"hours":"When it is open."},"knowledge":KNOWLEDGE}}}}
            """.Replace("KNOWLEDGE", knowledge, StringComparison.Ordinal);

    [Fact]
    [Trait("criterion", "AC-instruction-context-1")]
    public async Task An_entry_with_a_base_articles_and_queries_reads_back_as_written()
    {
        var scene = await NewBot();

        var saved = await scene.Procedures.Add(
            "orderValuation",
            Informing("""[{"knowledgeBase":"offer","articles":["officeContact"],"searchQueries":["how long a valuation takes"]}]""")
        );
        var read = await ProcedureApi.DetailOf(await scene.Procedures.Get("orderValuation"));

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var entry = Assert.Single(read.Steps["tell"].Inform?.Knowledge ?? []);
        Assert.Equal("offer", entry.KnowledgeBase);
        Assert.Equal(["officeContact"], entry.Articles);
        Assert.Equal(["how long a valuation takes"], entry.SearchQueries);
        Assert.Empty(read.NotReady);
    }

    [Theory]
    [Trait("criterion", "AC-instruction-context-2")]
    [InlineData("[]")]
    [InlineData("""[{"knowledgeBase":"offer","articles":["officeContact"]}]""")]
    [InlineData("""[{"knowledgeBase":"offer","searchQueries":["hours"]}]""")]
    public async Task An_empty_list_and_an_entry_with_one_key_alone_are_accepted_with_nothing_left_to_do(string knowledge)
    {
        var scene = await NewBot();

        var saved = await ProcedureApi.DetailOf(await scene.Procedures.Add("orderValuation", Informing(knowledge)));

        Assert.Empty(saved.NotReady);
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-3")]
    public async Task An_article_only_another_bot_has_is_refused_with_reference_missing_naming_where_the_entry_is()
    {
        var scene = await NewBot(withArticle: false);
        var other = await NewBot();

        var response = await scene.Procedures.Add(
            "orderValuation",
            Informing("""[{"knowledgeBase":"offer","articles":["officeContact"]}]""")
        );
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.NotEqual(scene.Articles.Bot, other.Articles.Bot);
        Assert.Equal("reference.missing", mistake.GetProperty("code").GetString());
        Assert.Contains("step 'tell'", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [Trait("criterion", "AC-instruction-context-4")]
    [InlineData("""[{"knowledgeBase":"offer","queries":["hours"]}]""", "queries")]
    public async Task A_key_an_entry_does_not_have_is_refused_naming_the_key(string knowledge, string key)
    {
        var scene = await NewBot();

        var mistake = Assert.Single(await FactApi.MistakesOf(await scene.Procedures.Add("orderValuation", Informing(knowledge))));

        Assert.Equal("save.invalid", mistake.GetProperty("code").GetString());
        Assert.Contains(key, mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(mistake.GetProperty("fix").GetString()));
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-4")]
    public async Task Context_on_an_instruction_is_refused_as_any_key_it_does_not_have()
    {
        var scene = await NewBot();
        const string body = """
            {"description":"Asks.","firstStep":"wait","steps":{"wait":{"listen":{}}},
             "whenStalled":{"instruction":"Say so.","context":{"articles":["officeContact"]}}}
            """;

        var mistake = Assert.Single(await FactApi.MistakesOf(await scene.Procedures.Add("orderValuation", body)));

        Assert.Equal("save.invalid", mistake.GetProperty("code").GetString());
        Assert.Contains("context", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-creator-50")]
    public async Task Knowledge_on_when_stalled_is_refused_as_any_key_it_does_not_have()
    {
        var scene = await NewBot();
        const string body = """
            {"description":"Asks.","firstStep":"wait","steps":{"wait":{"listen":{}}},
             "whenStalled":{"instruction":"Say so.","knowledge":[{"knowledgeBase":"offer","articles":["officeContact"]}]}}
            """;

        var mistake = Assert.Single(await FactApi.MistakesOf(await scene.Procedures.Add("orderValuation", body)));

        Assert.Equal("save.invalid", mistake.GetProperty("code").GetString());
        Assert.Contains("knowledge", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-5")]
    [Trait("criterion", "AC-instruction-context-6")]
    public async Task A_blank_query_or_a_query_with_no_articles_is_saved_listed_and_reported_when_creating_a_version_until_it_is_mended()
    {
        var scene = await NewBot(withArticle: false);
        var client = App.CreateClient();
        var versions = $"/api/bots/{scene.Articles.Bot}/versions";

        var saved = await ProcedureApi.DetailOf(
            await scene.Procedures.Add("orderValuation", Informing("""[{"knowledgeBase":"offer","searchQueries":["hours"," "]}]"""))
        );
        var refused = await client.PostAsync(versions, null, TestContext.Current.CancellationToken);

        Assert.Equal(["knowledge.query.blank", "knowledge.query.noArticles"], ProcedureApi.NotReadyCodes(saved));
        Assert.Equal(["knowledge.query.blank", "knowledge.query.noArticles"], await FactApi.CodesOf(refused));

        Assert.True((await scene.Articles.Add("officeContact", ArticleApi.Body())).IsSuccessStatusCode);
        var read = await ProcedureApi.DetailOf(await scene.Procedures.Get("orderValuation"));
        Assert.Equal(["knowledge.query.blank"], ProcedureApi.NotReadyCodes(read));
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-7")]
    public async Task A_query_is_saved_with_no_call_to_the_bot_and_reads_back_byte_for_byte()
    {
        _ = database.Pool;
        var client = App.CreateClient();
        using var listening = new TcpListener(IPAddress.Loopback, 0);
        listening.Start();
        var scene = await PushScene.RegisterAsync(
            client,
            $"q{Guid.NewGuid():N}",
            $"http://127.0.0.1:{((IPEndPoint)listening.LocalEndpoint).Port}"
        );
        var articles = new ArticleApi(client, scene.Bot);
        Assert.True((await articles.KnowledgeBases.Add("offer", "Offer")).IsSuccessStatusCode);
        var procedures = new ProcedureApi(client, new FactApi(client, scene.Bot));
        const string query = "  zażółć “gęślą”  [x](cms://article/none) `jaźń` ";

        var saved = await procedures.Add("orderValuation", Informing($$"""[{"knowledgeBase":"offer","searchQueries":["{{query}}"]}]"""));
        var read = await ProcedureApi.DetailOf(await procedures.Get("orderValuation"));

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.False(listening.Pending(), "A save of a query reached out to the bot's address.");
        Assert.Equal(["  zażółć “gęślą”  [x](cms://article/none) `jaźń` "], read.Steps["tell"].Inform?.Knowledge?[0].SearchQueries);
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-17")]
    public async Task An_article_an_entry_names_cannot_be_deleted_and_can_once_the_entry_is_gone()
    {
        var scene = await NewBot();
        Assert.True(
            (
                await scene.Procedures.Add("orderValuation", Informing("""[{"knowledgeBase":"offer","articles":["officeContact"]}]"""))
            ).IsSuccessStatusCode
        );

        var refused = await scene.Articles.Delete("officeContact");
        var related = Assert.Single(Assert.Single(await FactApi.MistakesOf(refused)).GetProperty("related").EnumerateArray());

        Assert.Equal("steps.tell.inform.knowledge[0].articles[0]", related.GetProperty("location").GetString());
        Assert.True((await scene.Procedures.Delete("orderValuation")).IsSuccessStatusCode);
        Assert.True((await scene.Articles.Delete("officeContact")).IsSuccessStatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-creator-34")]
    public async Task Moving_an_article_to_another_base_is_refused_while_an_entry_names_it_under_the_old_one()
    {
        var scene = await NewBot();
        Assert.True(
            (
                await scene.Procedures.Add("orderValuation", Informing("""[{"knowledgeBase":"offer","articles":["officeContact"]}]"""))
            ).IsSuccessStatusCode
        );

        var moved = await scene.Articles.Put("officeContact", ArticleApi.Body(knowledgeBase: "forValuers"));

        Assert.Equal(["knowledge.article.otherBase"], await FactApi.CodesOf(moved));
    }

    [Fact]
    [Trait("criterion", "AC-procedure-creator-36")]
    public async Task A_base_an_entry_names_cannot_be_deleted_shows_in_the_panels_and_its_new_id_rewrites_the_entry()
    {
        var scene = await NewBot(withArticle: false);
        var client = App.CreateClient();
        var bot = scene.Articles.Bot;
        var ask = TestContext.Current.CancellationToken;
        Assert.True(
            (
                await scene.Procedures.Add("orderValuation", Informing("""[{"knowledgeBase":"offer","searchQueries":["hours"]}]"""))
            ).IsSuccessStatusCode
        );

        var refused = await scene.Articles.KnowledgeBases.Delete("offer");
        var related = Assert.Single(Assert.Single(await FactApi.MistakesOf(refused)).GetProperty("related").EnumerateArray());
        var uses = await client.GetStringAsync($"/api/bots/{bot}/procedures/orderValuation/usage", ask);
        using var newId = new StringContent("""{"newId":"prices"}""", System.Text.Encoding.UTF8, "application/json");
        var renamed = await client.PostAsync($"/api/bots/{bot}/knowledge-bases/offer/rename", newId, ask);
        var read = await ProcedureApi.DetailOf(await scene.Procedures.Get("orderValuation"));

        Assert.Equal("steps.tell.inform.knowledge[0].knowledgeBase", related.GetProperty("location").GetString());
        Assert.Contains("\"offer\"", uses, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("prices", read.Steps["tell"].Inform?.Knowledge?[0].KnowledgeBase);
    }
}
