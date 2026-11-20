using System.Net;
using Dapper;
using Suwagcms.Server.Tests.Facts;

namespace Suwagcms.Server.Tests.KnowledgeBases;

public sealed class KnowledgeBaseLanguageTests(TestDatabase database, ClassServer server) : IClassFixture<ClassServer>
{
    private TestApp App => server.App;

    private async Task<KnowledgeBaseApi> NewBot()
    {
        _ = database.DataSource;
        return await KnowledgeBaseApi.NewBotAsync(App.CreateClient());
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-1")]
    public async Task A_base_added_without_a_language_has_en_and_reading_it_back_shows_en()
    {
        var api = await NewBot();

        var saved = await KnowledgeBaseApi.ViewOf(await api.Add("offer", "Offer"));

        Assert.Equal("en", saved.Language);
        Assert.Equal("en", (await KnowledgeBaseApi.ViewOf(await api.Get("offer"))).Language);
        Assert.Equal("en", Assert.Single(await api.List()).Language);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-1")]
    public async Task A_stored_base_without_a_language_reads_as_en()
    {
        var api = await NewBot();
        await using var connection = await database.DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await connection.ExecuteAsync(
            "INSERT INTO resource (id, bot_id, kind, public_id, revision, body, updated_at) VALUES (@id, @bot, 'knowledgeBase', 'old', 1, @body::jsonb, now())",
            new
            {
                id = Guid.NewGuid(),
                bot = api.Bot,
                body = """{"id":"old","title":"Old","embedding":"default","results":5}""",
            }
        );

        Assert.Equal("en", (await KnowledgeBaseApi.ViewOf(await api.Get("old"))).Language);
    }

    [Theory]
    [Trait("criterion", "AC-knowledge-base-language-1")]
    [InlineData("pl")]
    [InlineData("en")]
    public async Task A_base_added_with_a_language_keeps_it(string language)
    {
        var api = await NewBot();

        await api.AddWithLanguage("offer", "Offer", language);

        Assert.Equal(language, (await KnowledgeBaseApi.ViewOf(await api.Get("offer"))).Language);
    }

    [Theory]
    [Trait("criterion", "AC-knowledge-base-language-4")]
    [InlineData("PL")]
    [InlineData("de")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Any_other_language_is_refused_at_the_save_stage_listing_both_and_nothing_is_saved(string language)
    {
        var api = await NewBot();

        var response = await api.AddWithLanguage("oferta", "Oferta", language);
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("knowledgeBase.language.unknown", mistake.GetProperty("code").GetString());
        Assert.Equal("language", mistake.GetProperty("path").GetString());
        Assert.Equal("Use one of: pl, en.", mistake.GetProperty("fix").GetString());
        Assert.Empty(await api.List());
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-4")]
    public async Task Changing_the_language_of_a_base_to_another_value_is_refused_and_the_base_keeps_its_own()
    {
        var api = await NewBot();
        await api.AddWithLanguage("oferta", "Oferta", "pl");

        var response = await api.Put("oferta", KnowledgeBaseApi.Body("Oferta", "de"));

        var mistake = Assert.Single(await FactApi.MistakesOf(response));
        Assert.Equal("Knowledge base 'oferta' has language 'de'.", mistake.GetProperty("message").GetString());
        Assert.Equal("pl", Assert.Single(await api.List()).Language);
    }
}
