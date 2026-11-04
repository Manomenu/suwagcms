using System.Net;
using Suwagcms.Server.Tests.Facts;

namespace Suwagcms.Server.Tests.Articles;

public sealed class ArticleLanguageTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    private async Task<ArticleApi> NewBot()
    {
        _ = database.Pool;
        return await ArticleApi.NewBotAsync(app.CreateClient());
    }

    [Fact]
    [Trait("criterion", "AC-article-language-1")]
    public async Task An_article_added_without_a_language_has_en_and_reading_it_back_shows_en()
    {
        var api = await NewBot();

        var saved = await ArticleApi.ViewOf(await api.Add("priceList", ArticleApi.Body()));

        Assert.Equal("en", saved.Language);
        Assert.Equal("en", (await ArticleApi.ViewOf(await api.Get("priceList"))).Language);
        Assert.Equal("en", Assert.Single(await api.List()).Language);
    }

    [Theory]
    [Trait("criterion", "AC-article-language-1")]
    [InlineData("pl")]
    [InlineData("en")]
    public async Task An_article_added_with_a_language_keeps_it(string language)
    {
        var api = await NewBot();

        await api.Add("priceList", ArticleApi.Body(language: language));

        Assert.Equal(language, (await ArticleApi.ViewOf(await api.Get("priceList"))).Language);
    }

    [Theory]
    [Trait("criterion", "AC-article-language-4")]
    [InlineData("PL")]
    [InlineData("de")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Any_other_language_is_refused_listing_both_and_nothing_is_saved(string language)
    {
        var api = await NewBot();

        var response = await api.Add("cennik", ArticleApi.Body(language: language));
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("article.language.unknown", mistake.GetProperty("code").GetString());
        Assert.Equal("language", mistake.GetProperty("path").GetString());
        Assert.Equal("Use one of: pl, en.", mistake.GetProperty("fix").GetString());
        Assert.Empty(await api.List());
    }

    [Fact]
    [Trait("criterion", "AC-article-language-4")]
    public async Task Changing_the_language_of_an_article_to_another_value_is_refused_and_the_article_keeps_its_own()
    {
        var api = await NewBot();
        await api.Add("cennik", ArticleApi.Body(language: "pl"));

        var response = await api.Put("cennik", ArticleApi.Body(language: "de"));

        var mistake = Assert.Single(await FactApi.MistakesOf(response));
        Assert.Equal("'cennik' has language 'de'.", mistake.GetProperty("message").GetString());
        Assert.Equal("pl", Assert.Single(await api.List()).Language);
    }

    [Fact]
    [Trait("criterion", "AC-article-language-5")]
    public async Task An_edit_without_a_language_keeps_the_one_the_article_has_and_en_does_not_overwrite_pl()
    {
        var api = await NewBot();
        await api.Add("cennik", ArticleApi.Body(language: "pl"));

        var withoutRevision = await ArticleApi.ViewOf(await api.Put("cennik", ArticleApi.Body(title: "New title")));
        var withRevision = await ArticleApi.ViewOf(await api.Put("cennik", ArticleApi.Body(title: "Newer title"), "?ifRevision=2"));

        Assert.Equal(("pl", "New title", 2), (withoutRevision.Language, withoutRevision.Title, withoutRevision.Revision));
        Assert.Equal(("pl", "Newer title", 3), (withRevision.Language, withRevision.Title, withRevision.Revision));
        Assert.Equal("pl", Assert.Single(await api.List()).Language);
    }

    [Fact]
    [Trait("criterion", "AC-article-language-5")]
    public async Task An_edit_that_gives_a_language_changes_it_and_a_stale_edit_without_one_is_still_refused()
    {
        var api = await NewBot();
        await api.Add("cennik", ArticleApi.Body(language: "pl"));

        var changed = await ArticleApi.ViewOf(await api.Put("cennik", ArticleApi.Body(language: "en")));
        var stale = await api.Put("cennik", ArticleApi.Body(title: "Stale"), "?ifRevision=1");

        Assert.Equal("en", changed.Language);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("en", Assert.Single(await api.List()).Language);
    }

    [Fact]
    [Trait("criterion", "AC-article-language-6")]
    public async Task The_list_filters_by_language_together_with_the_base_and_the_text()
    {
        var api = await NewBot();
        await api.Add("cennik", ArticleApi.Body(language: "pl", title: "Cennik", description: "Ile kosztuje wycena"));
        await api.Add("pricing", ArticleApi.Body(title: "Pricing", description: "What a valuation costs"));
        await api.Add("cennikRzeczoznawcy", ArticleApi.Body("forValuers", "pl", "Cennik", description: "Ile kosztuje wycena"));

        Assert.Equal(["cennik", "cennikRzeczoznawcy"], (await api.List("?language=pl")).Select(article => article.Id));
        Assert.Equal(["pricing"], (await api.List("?language=en")).Select(article => article.Id));
        Assert.Equal(["cennik"], (await api.List("?language=pl&knowledgeBase=offer")).Select(article => article.Id));
        Assert.Equal(["cennik", "cennikRzeczoznawcy"], (await api.List("?language=pl&text=CENNIK")).Select(article => article.Id));
        Assert.Empty(await api.List("?language=pl&text=pricing"));
        Assert.Equal(3, (await api.List("?language=")).Count);
    }

    [Fact]
    [Trait("criterion", "AC-article-language-7")]
    public async Task Two_articles_in_different_languages_share_a_title_and_editing_or_deleting_one_changes_nothing_in_the_other()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("cennik", ArticleApi.Body(language: "pl", title: "Cennik"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Add("pricing", ArticleApi.Body(language: "en", title: "Cennik"))).StatusCode);

        await api.Put("cennik", ArticleApi.Body(language: "pl", title: "Cennik 2"));
        var untouched = await ArticleApi.ViewOf(await api.Get("pricing"));
        Assert.Equal(("Cennik", "en", 1), (untouched.Title, untouched.Language, untouched.Revision));

        Assert.Equal(HttpStatusCode.NoContent, (await api.Delete("pricing")).StatusCode);
        var remaining = Assert.Single(await api.List());
        Assert.Equal(("cennik", "Cennik 2", "pl", 2), (remaining.Id, remaining.Title, remaining.Language, remaining.Revision));
    }
}
