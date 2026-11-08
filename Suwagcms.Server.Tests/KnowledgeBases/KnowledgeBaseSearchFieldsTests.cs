using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Tests.Changes;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.KnowledgeBases;

public sealed class KnowledgeBaseSearchFieldsTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    private async Task<KnowledgeBaseApi> NewBot()
    {
        _ = database.Pool;
        return await KnowledgeBaseApi.NewBotAsync(app.CreateClient());
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-15")]
    public async Task A_base_without_a_profile_or_results_is_refused_with_both_mistakes_and_nothing_is_saved()
    {
        var api = await NewBot();

        var response = await api.Put("offer", """{"title":"Offer"}""", "?ifRevision=0");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(
            [MistakeCodes.KnowledgeBaseEmbeddingMissing, MistakeCodes.KnowledgeBaseResultsOutOfRange],
            (await FactApi.CodesOf(response)).Order(StringComparer.Ordinal)
        );
        Assert.Empty(await api.List());
    }

    [Theory]
    [Trait("criterion", "AC-knowledge-bases-15")]
    [InlineData("""{"title":"Offer","embedding":" ","results":5}""", "knowledgeBase.embedding.missing")]
    [InlineData("""{"title":"Offer","embedding":"default","results":21}""", "knowledgeBase.results.outOfRange")]
    public async Task A_blank_profile_or_results_outside_1_to_20_are_refused_with_the_fix(string body, string code)
    {
        var api = await NewBot();

        var response = await api.Put("offer", body, "?ifRevision=0");

        var mistake = Assert.Single(await FactApi.MistakesOf(response));
        Assert.Equal(code, mistake.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(mistake.GetProperty("fix").GetString()));
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-15")]
    public async Task Any_profile_name_saves_since_a_save_never_asks_the_bot()
    {
        var api = await NewBot();

        var saved = await KnowledgeBaseApi.ViewOf(
            await api.Put("legal", """{"title":"Legal","embedding":"legalText","results":20}""", "?ifRevision=0")
        );

        Assert.Equal(("legalText", 20), (saved.Embedding, saved.Results));
        Assert.Equal(("legalText", 20), (await api.List()).Select(item => (item.Embedding, item.Results)).Single());
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-17")]
    public async Task A_version_carries_the_profile_and_results_of_each_base()
    {
        _ = database.Pool;
        var versions = await (await VersionApi.NewBotAsync(app.CreateClient())).Ready();
        var bases = new KnowledgeBaseApi(versions.Client, versions.Bot);
        Assert.True(
            (await bases.Put("legal", """{"title":"Legal","embedding":"legalText","results":12}""", "?ifRevision=0")).IsSuccessStatusCode
        );

        var document = await versions.DocumentOf((await versions.Created()).Version.Number);

        var carried = Assert.Single(document.GetProperty("knowledgeBases").EnumerateArray());
        Assert.Equal(("legalText", 12), (carried.GetProperty("embedding").GetString(), carried.GetProperty("results").GetInt32()));
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-17")]
    public async Task A_file_of_changes_adds_a_base_with_both_and_edits_one_alone()
    {
        _ = database.Pool;
        var scene = await ChangeScene.NewAsync(app.CreateClient());
        var bases = new KnowledgeBaseApi(app.CreateClient(), scene.Bot);

        Assert.True(
            (
                await scene.Post(
                    ChangeScene.File("""{"add":"knowledgeBase","id":"legal","title":"Legal","embedding":"legalText","results":12}""")
                )
            ).IsSuccessStatusCode
        );
        Assert.True((await scene.Post(ChangeScene.File("""{"edit":"knowledgeBase","id":"legal","results":3}"""))).IsSuccessStatusCode);

        var saved = Assert.Single(await bases.List());
        Assert.Equal(("Legal", "legalText", 3), (saved.Title, saved.Embedding, saved.Results));
    }

    [Fact]
    [Trait("criterion", "AC-bot-declaration-22")]
    public async Task Creating_a_version_whose_base_names_a_profile_the_bot_does_not_serve_is_refused_and_stores_nothing()
    {
        _ = database.Pool;
        var client = app.CreateClient();
        var id = PushScene.FreshId();
        await using var bot = await TestBot.StartAsync(
            id,
            declares: new BotCapabilities
            {
                FormatVersion = 4,
                Blocks = Block.Names,
                EmbeddingProfiles = ["default"],
            }
        );
        var scene = await PushScene.RegisterAsync(client, id, bot.Address);
        await scene.FillAsync();
        Assert.True(
            (
                await client.GetAsync(new Uri($"/api/bots/{id}/declaration", UriKind.Relative), TestContext.Current.CancellationToken)
            ).IsSuccessStatusCode
        );
        Assert.True(
            (
                await new KnowledgeBaseApi(client, id).Put(
                    "offer",
                    """{"title":"Offer","embedding":"legalText","results":5}""",
                    "?ifRevision=1"
                )
            ).IsSuccessStatusCode
        );

        var refused = await client.PostAsync($"/api/bots/{id}/versions", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var mistake = Assert.Single(await FactApi.MistakesOf(refused));
        Assert.Equal(MistakeCodes.DeclarationEmbeddingProfile, mistake.GetProperty("code").GetString());
        Assert.Contains("It serves: default.", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Empty(
            await client.GetFromJsonAsync<List<JsonElement>>($"/api/bots/{id}/versions", TestContext.Current.CancellationToken) ?? []
        );
    }
}
