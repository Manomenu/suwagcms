using System.Net;
using System.Text.Json;
using Suwagcms.Server.Tests.Facts;

namespace Suwagcms.Server.Tests.Articles;

public sealed class ArticleLoopTests(TestDatabase database, SharedApp shared) : IClassFixture<SharedApp>
{
    private TestApp App => shared.App;

    private async Task<ArticleApi> BotWithPricingAndDiscounts()
    {
        _ = database.Pool;
        var api = await ArticleApi.NewBotAsync(App.CreateClient());
        Assert.Equal(HttpStatusCode.OK, (await api.Add("pricing", ArticleApi.Body())).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await api.Add("discounts", ArticleApi.Body(content: "See [Pricing](cms://article/pricing)."))).StatusCode
        );
        return api;
    }

    private static string Closing => ArticleApi.Body(content: "See [Discounts](cms://article/discounts).");

    [Fact]
    [Trait("criterion", "AC-loop-guard-1")]
    [Trait("criterion", "AC-loop-guard-2")]
    public async Task A_link_that_closes_a_loop_is_refused_through_the_api_with_the_path_as_text_and_as_data_and_nothing_is_saved()
    {
        var api = await BotWithPricingAndDiscounts();

        var response = await api.Put("pricing", Closing, "?ifRevision=1");
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("reference.loop", mistake.GetProperty("code").GetString());
        Assert.Contains("discounts → pricing → discounts", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal("discounts", mistake.GetProperty("resource").GetProperty("id").GetString());
        var related = Assert.Single(mistake.GetProperty("related").EnumerateArray());
        Assert.Equal("pricing", related.GetProperty("resource").GetProperty("id").GetString());
        Assert.Equal("content", related.GetProperty("location").GetString());
        var stored = await ArticleApi.ViewOf(await api.Get("pricing"));
        Assert.Equal(1, stored.Revision);
        Assert.Equal("What a valuation costs.", stored.Content);
    }

    [Fact]
    [Trait("criterion", "AC-loop-guard-3")]
    public async Task An_article_added_with_a_link_to_itself_is_refused_with_its_own_path_and_not_stored()
    {
        _ = database.Pool;
        var api = await ArticleApi.NewBotAsync(App.CreateClient());

        var response = await api.Add("pricing", ArticleApi.Body(llmGuide: "Also [me](cms://article/pricing)."));
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("reference.loop", mistake.GetProperty("code").GetString());
        Assert.Equal("llmGuide", mistake.GetProperty("path").GetString());
        Assert.Contains("pricing → pricing", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Empty(await api.List());
    }

    [Fact]
    [Trait("criterion", "AC-loop-guard-11")]
    public async Task A_dry_run_of_a_link_that_closes_a_loop_gives_the_same_refusal_and_changes_nothing()
    {
        var api = await BotWithPricingAndDiscounts();

        var dry = await api.Put("pricing", Closing, "?ifRevision=1&dryRun=true");
        var real = await api.Put("pricing", Closing, "?ifRevision=1");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, dry.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(await FactApi.MistakesOf(dry)), JsonSerializer.Serialize(await FactApi.MistakesOf(real)));
        Assert.Equal("reference.loop", Assert.Single(await FactApi.MistakesOf(dry)).GetProperty("code").GetString());
        Assert.Equal(1, (await ArticleApi.ViewOf(await api.Get("pricing"))).Revision);
    }
}
