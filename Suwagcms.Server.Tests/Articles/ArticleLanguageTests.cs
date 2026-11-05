using System.Net;
using System.Text.Json;
using Dapper;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.KnowledgeBases;

namespace Suwagcms.Server.Tests.Articles;

public sealed class ArticleLanguageTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    // Why: the bot has the bases offer and forValuers, both en, and oferta, which is pl.
    private async Task<ArticleApi> NewBot()
    {
        _ = database.Pool;
        var api = await ArticleApi.NewBotAsync(app.CreateClient());
        (await api.KnowledgeBases.AddWithLanguage("oferta", "Oferta", "pl")).EnsureSuccessStatusCode();
        return api;
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-6")]
    public async Task An_article_reads_with_the_language_of_its_base_when_saved_shown_and_listed()
    {
        var api = await NewBot();

        var saved = await ArticleApi.ViewOf(await api.Add("cennik", ArticleApi.Body("oferta")));
        var english = await ArticleApi.ViewOf(await api.Add("pricing", ArticleApi.Body()));

        Assert.Equal("pl", saved.Language);
        Assert.Equal("pl", (await ArticleApi.ViewOf(await api.Get("cennik"))).Language);
        Assert.Equal("en", english.Language);
        Assert.Equal(["cennik|pl", "pricing|en"], (await api.List()).Select(article => $"{article.Id}|{article.Language}"));
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-6")]
    public async Task Changing_the_language_of_a_base_changes_it_for_all_its_articles_and_rewrites_none()
    {
        var api = await NewBot();
        await api.Add("cennik", ArticleApi.Body("oferta"));
        await api.Add("rabaty", ArticleApi.Body("oferta", title: "Rabaty"));

        var changed = await api.KnowledgeBases.Put("oferta", KnowledgeBaseApi.Body("Oferta", "en"));

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var articles = await api.List("?knowledgeBase=oferta");
        Assert.Equal(["cennik|en|1", "rabaty|en|1"], articles.Select(article => $"{article.Id}|{article.Language}|{article.Revision}"));
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-6")]
    public async Task Moving_an_article_to_a_base_of_another_language_changes_its_language()
    {
        var api = await NewBot();
        await api.Add("cennik", ArticleApi.Body("oferta"));

        var toEnglish = await ArticleApi.ViewOf(await api.Put("cennik", ArticleApi.Body("offer")));
        var backToPolish = await ArticleApi.ViewOf(await api.Put("cennik", ArticleApi.Body("oferta")));

        Assert.Equal("en", toEnglish.Language);
        Assert.Equal("pl", backToPolish.Language);
        Assert.Equal("pl", Assert.Single(await api.List()).Language);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-7")]
    public async Task A_save_that_sends_a_language_on_an_article_is_refused_as_an_unknown_field_and_saves_nothing()
    {
        var api = await NewBot();

        var response = await api.Add(
            "cennik",
            """{"knowledgeBase":"oferta","language":"pl","title":"Cennik","content":"c","llmGuide":"g","description":"d"}"""
        );
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("save.unknownField", mistake.GetProperty("code").GetString());
        Assert.Equal("language", mistake.GetProperty("path").GetString());
        Assert.Empty(await api.List());
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-8")]
    public async Task A_stored_article_body_holding_a_language_still_loads_with_the_field_ignored_and_does_not_write_it_back()
    {
        var api = await NewBot();
        await using var connection = await database.Pool.OpenAsync(TestContext.Current.CancellationToken);
        await connection.ExecuteAsync(
            "INSERT INTO resource (id, bot_id, kind, public_id, revision, body, updated_at) VALUES (@id, @bot, 'article', 'cennik', 1, @body::jsonb, now())",
            new
            {
                id = Guid.NewGuid(),
                bot = api.Bot,
                body = """{"id":"cennik","knowledgeBase":"oferta","language":"en","title":"Cennik","content":"c","llmGuide":"g","description":"d"}""",
            }
        );

        var loaded = await ArticleApi.ViewOf(await api.Get("cennik"));
        var saved = await api.Put("cennik", ArticleApi.Body("oferta", title: "Cennik 2"), "?ifRevision=1");
        var stored = await connection.ExecuteScalarAsync<string>(
            "SELECT body::text FROM resource WHERE bot_id = @bot AND public_id = 'cennik'",
            new { bot = api.Bot }
        );

        Assert.Equal(("pl", "Cennik"), (loaded.Language, loaded.Title));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var body = JsonDocument.Parse(stored ?? "");
        Assert.False(body.RootElement.TryGetProperty("language", out _));
        Assert.Equal("Cennik 2", body.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-9")]
    public async Task The_list_filters_by_language_together_with_the_base_and_the_text()
    {
        var api = await NewBot();
        await api.Add("cennik", ArticleApi.Body("oferta", "Cennik", description: "Ile kosztuje wycena"));
        await api.Add("pricing", ArticleApi.Body(title: "Pricing", description: "What a valuation costs"));
        await api.Add("cennikRzeczoznawcy", ArticleApi.Body("oferta", "Cennik rzeczoznawcy", description: "Ile kosztuje wycena"));

        Assert.Equal(["cennik", "cennikRzeczoznawcy"], (await api.List("?language=pl")).Select(article => article.Id));
        Assert.Equal(["pricing"], (await api.List("?language=en")).Select(article => article.Id));
        Assert.Empty(await api.List("?language=pl&knowledgeBase=offer"));
        Assert.Equal(["cennik", "cennikRzeczoznawcy"], (await api.List("?language=pl&text=CENNIK")).Select(article => article.Id));
        Assert.Empty(await api.List("?language=pl&text=pricing"));
        Assert.Equal(3, (await api.List("?language=")).Count);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-10")]
    public async Task Two_articles_in_different_languages_share_a_title_and_editing_or_deleting_one_changes_nothing_in_the_other()
    {
        var api = await NewBot();
        Assert.Equal(HttpStatusCode.OK, (await api.Add("cennik", ArticleApi.Body("oferta", title: "Cennik"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Add("pricing", ArticleApi.Body(title: "Cennik"))).StatusCode);

        await api.Put("cennik", ArticleApi.Body("oferta", title: "Cennik 2"));
        var untouched = await ArticleApi.ViewOf(await api.Get("pricing"));
        Assert.Equal(("Cennik", "en", 1), (untouched.Title, untouched.Language, untouched.Revision));

        Assert.Equal(HttpStatusCode.NoContent, (await api.Delete("pricing")).StatusCode);
        var remaining = Assert.Single(await api.List());
        Assert.Equal(("cennik", "Cennik 2", "pl", 2), (remaining.Id, remaining.Title, remaining.Language, remaining.Revision));
    }
}
