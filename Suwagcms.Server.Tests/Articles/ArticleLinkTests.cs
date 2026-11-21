using System.Net;
using Suwagcms.Server.Tests.Facts;

namespace Suwagcms.Server.Tests.Articles;

public sealed class ArticleLinkTests(TestDatabase database, ClassServer server) : IClassFixture<ClassServer>
{
    private TestApp App => server.App;

    private async Task<ArticleApi> NewBot()
    {
        _ = database.DataSource;
        return await ArticleApi.NewBotAsync(App.CreateClient());
    }

    [Fact]
    [Trait("criterion", "AC-article-links-7")]
    public async Task A_link_to_an_article_the_bot_does_not_have_is_refused_with_the_field_and_the_id_and_nothing_is_saved()
    {
        var api = await NewBot();
        await api.Add("cennik", ArticleApi.Body(title: "Price list"));

        var response = await api.Add("wycena", ArticleApi.Body(content: "See the [Price list](cms://article/cenik)."));
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("reference.missing", mistake.GetProperty("code").GetString());
        Assert.Equal("content", mistake.GetProperty("at").GetString());
        Assert.Contains("links to article 'cenik'", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(["cennik"], (await api.List()).Select(article => article.Id));
    }

    [Fact]
    [Trait("criterion", "AC-article-links-7")]
    public async Task A_link_to_an_id_that_exists_only_in_another_bot_is_refused()
    {
        var mine = await NewBot();
        var other = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await other.Add("secret", ArticleApi.Body())).StatusCode);

        var response = await mine.Add("wycena", ArticleApi.Body(content: "See [it](cms://article/secret)."));
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("reference.missing", mistake.GetProperty("code").GetString());
        Assert.Empty(await mine.List());
    }

    [Fact]
    [Trait("criterion", "AC-article-links-11")]
    public async Task A_link_to_an_article_in_another_base_of_the_bot_is_saved_exactly_as_written()
    {
        var api = await NewBot();
        await api.Add("cennik", ArticleApi.Body("forValuers"));
        const string content = "See the [Price list](cms://article/cennik).\r\nNext line.";

        var response = await api.Add("wycena", ArticleApi.Body("offer", content: content, llmGuide: "Also [x](cms://article/cennik)."));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(content, (await ArticleApi.ViewOf(await api.Get("wycena"))).Content);
    }
}
