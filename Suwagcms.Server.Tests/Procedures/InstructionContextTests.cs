using System.Net;
using System.Net.Sockets;
using Suwagcms.Server.Tests.Articles;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.Procedures;

public sealed class InstructionContextTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    private sealed record Scene(ArticleApi Articles, ProcedureApi Procedures);

    private async Task<Scene> NewBot(bool withArticle = true)
    {
        _ = database.Pool;
        var client = app.CreateClient();
        var articles = await ArticleApi.NewBotAsync(client);
        if (withArticle)
        {
            Assert.True((await articles.Add("officeContact", ArticleApi.Body(knowledgeBase: "forValuers"))).IsSuccessStatusCode);
        }
        var procedures = new ProcedureApi(client, new FactApi(client, articles.Bot));
        Assert.True((await procedures.Add("conversation", ProcedureApi.Conversation)).IsSuccessStatusCode);
        return new Scene(articles, procedures);
    }

    private static string Stalling(string context) =>
        """
            {"description":"Asks.","firstStep":"wait","steps":{"wait":{"listen":{}}},
             "whenStalled":{"instruction":"Say how to reach the office.","context":CONTEXT}}
            """.Replace("CONTEXT", context, StringComparison.Ordinal);

    [Fact]
    [Trait("criterion", "AC-instruction-context-1")]
    [Trait("criterion", "AC-instruction-context-3")]
    public async Task A_context_naming_an_article_of_another_base_and_queries_reads_back_as_written()
    {
        var scene = await NewBot();

        var saved = await scene.Procedures.Add(
            "orderValuation",
            Stalling("""{"articles":["officeContact"],"searchQueries":["how long a valuation takes"]}""")
        );
        var read = await ProcedureApi.DetailOf(await scene.Procedures.Get("orderValuation"));

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(["officeContact"], read.WhenStalled?.Context?.Articles);
        Assert.Equal(["how long a valuation takes"], read.WhenStalled?.Context?.SearchQueries);
        Assert.Empty(read.NotReady);
    }

    [Theory]
    [Trait("criterion", "AC-instruction-context-2")]
    [InlineData("{}")]
    [InlineData("""{"articles":[],"searchQueries":[]}""")]
    [InlineData("""{"articles":["officeContact"]}""")]
    [InlineData("""{"searchQueries":["hours"]}""")]
    public async Task An_empty_context_empty_lists_and_one_key_alone_are_accepted_with_nothing_left_to_do(string context)
    {
        var scene = await NewBot();

        var saved = await ProcedureApi.DetailOf(await scene.Procedures.Add("orderValuation", Stalling(context)));

        Assert.Empty(saved.NotReady);
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-3")]
    public async Task An_article_only_another_bot_has_is_refused_with_reference_missing_naming_where_the_context_is()
    {
        var scene = await NewBot(withArticle: false);
        var other = await NewBot();

        var response = await scene.Procedures.Add("orderValuation", Stalling("""{"articles":["officeContact"]}"""));
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.NotEqual(scene.Articles.Bot, other.Articles.Bot);
        Assert.Equal("reference.missing", mistake.GetProperty("code").GetString());
        Assert.Contains("whenStalled", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-4")]
    public async Task A_key_context_does_not_have_is_refused_naming_the_key()
    {
        var scene = await NewBot();

        var mistake = Assert.Single(
            await FactApi.MistakesOf(await scene.Procedures.Add("orderValuation", Stalling("""{"queries":["hours"]}""")))
        );

        Assert.Equal("save.invalid", mistake.GetProperty("code").GetString());
        Assert.Contains("queries", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(mistake.GetProperty("fix").GetString()));
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-5")]
    [Trait("criterion", "AC-instruction-context-6")]
    public async Task A_blank_query_or_a_query_with_no_articles_is_saved_listed_and_reported_when_creating_a_version_until_it_is_mended()
    {
        var scene = await NewBot(withArticle: false);
        var client = app.CreateClient();
        var versions = $"/api/bots/{scene.Articles.Bot}/versions";

        var saved = await ProcedureApi.DetailOf(
            await scene.Procedures.Add("orderValuation", Stalling("""{"searchQueries":["hours"," "]}"""))
        );
        var refused = await client.PostAsync(versions, null, TestContext.Current.CancellationToken);

        Assert.Equal(["context.query.blank", "context.query.noArticles"], ProcedureApi.NotReadyCodes(saved));
        Assert.Equal(["context.query.blank", "context.query.noArticles"], await FactApi.CodesOf(refused));

        Assert.True((await scene.Articles.Add("officeContact", ArticleApi.Body())).IsSuccessStatusCode);
        var read = await ProcedureApi.DetailOf(await scene.Procedures.Get("orderValuation"));
        Assert.Equal(["context.query.blank"], ProcedureApi.NotReadyCodes(read));
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-7")]
    public async Task A_query_is_saved_with_no_call_to_the_bot_and_reads_back_byte_for_byte()
    {
        _ = database.Pool;
        var client = app.CreateClient();
        using var listening = new TcpListener(IPAddress.Loopback, 0);
        listening.Start();
        var scene = await PushScene.RegisterAsync(
            client,
            $"q{Guid.NewGuid():N}",
            $"http://127.0.0.1:{((IPEndPoint)listening.LocalEndpoint).Port}"
        );
        var procedures = new ProcedureApi(client, new FactApi(client, scene.Bot));
        const string query = "  zażółć \u201Cgęślą\u201D  [x](cms://article/none) `jaźń` ";

        var saved = await procedures.Add("orderValuation", Stalling($$"""{"searchQueries":["{{query}}"]}"""));
        var read = await ProcedureApi.DetailOf(await procedures.Get("orderValuation"));

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.False(listening.Pending(), "A save of a query reached out to the bot's address.");
        Assert.Equal(["  zażółć \u201Cgęślą\u201D  [x](cms://article/none) `jaźń` "], read.WhenStalled?.Context?.SearchQueries);
    }

    [Fact]
    [Trait("criterion", "AC-instruction-context-10")]
    public async Task The_instruction_of_an_inform_takes_a_context_under_the_same_rules()
    {
        var scene = await NewBot();
        const string inform = """
            {"description":"Tells.","firstStep":"tell","steps":{"tell":{"inform":
              {"instruction":"Tell it.","points":{"hours":"When it is open."},"context":{"articles":["ARTICLE"],"searchQueries":["hours"]}}}}}
            """;

        var refused = await scene.Procedures.Add("orderValuation", inform.Replace("ARTICLE", "ghost", StringComparison.Ordinal));
        var saved = await ProcedureApi.DetailOf(
            await scene.Procedures.Add("orderValuation", inform.Replace("ARTICLE", "officeContact", StringComparison.Ordinal))
        );
        var read = await ProcedureApi.DetailOf(await scene.Procedures.Get("orderValuation"));

        Assert.Equal(["reference.missing"], await ProcedureApi.CodesOf(refused));
        Assert.Empty(saved.NotReady);
        Assert.Equal(["hours"], read.Steps["tell"].Inform?.Context?.SearchQueries);
    }

    [Fact]
    [Trait("criterion", "AC-delete-guard-17")]
    public async Task An_article_a_context_names_cannot_be_deleted_and_can_once_the_context_is_gone()
    {
        var scene = await NewBot();
        Assert.True((await scene.Procedures.Add("orderValuation", Stalling("""{"articles":["officeContact"]}"""))).IsSuccessStatusCode);

        var refused = await scene.Articles.Delete("officeContact");
        var related = Assert.Single(Assert.Single(await FactApi.MistakesOf(refused)).GetProperty("related").EnumerateArray());

        Assert.Equal("whenStalled.context.articles[0]", related.GetProperty("location").GetString());
        Assert.True((await scene.Procedures.Delete("orderValuation")).IsSuccessStatusCode);
        Assert.True((await scene.Articles.Delete("officeContact")).IsSuccessStatusCode);
    }
}
