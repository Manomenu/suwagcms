using System.Net;
using System.Text.Json;
using Suwagcms.Server.Tests.Facts;

namespace Suwagcms.Server.Tests.Articles;

public sealed class ArticleEndpointsTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    private async Task<ArticleApi> NewBot()
    {
        _ = database.Pool;
        return await ArticleApi.NewBotAsync(app.CreateClient());
    }

    private static string[] Codes(IEnumerable<JsonElement> mistakes) =>
        [.. mistakes.Select(mistake => mistake.GetProperty("code").GetString() ?? "").Order(StringComparer.Ordinal)];

    [Fact]
    [Trait("criterion", "AC-articles-1")]
    public async Task An_article_that_is_added_is_listed_in_its_base_and_shown_with_every_field_and_its_revision()
    {
        var api = await NewBot();

        Assert.Equal(HttpStatusCode.OK, (await api.Add("priceList", ArticleApi.Body())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Add("valuerGuide", ArticleApi.Body("forValuers", title: "Guide"))).StatusCode);

        Assert.Equal(["priceList", "valuerGuide"], (await api.List()).Select(article => article.Id));
        Assert.Equal(["priceList"], (await api.List("?knowledgeBase=offer")).Select(article => article.Id));
        Assert.Equal(
            "priceList|offer|en|Price list|What a valuation costs.|Ask which kind of property first.|What a valuation costs and what affects the price|1",
            ArticleApi.Describe(await ArticleApi.ViewOf(await api.Get("priceList")))
        );
    }

    [Fact]
    [Trait("criterion", "AC-articles-2")]
    public async Task A_missing_or_blank_title_content_guide_and_description_are_all_reported_at_once_with_fixes_and_nothing_is_saved()
    {
        var api = await NewBot();

        var blank = await api.Add("priceList", ArticleApi.Body(title: " ", content: "", llmGuide: "\n", description: "  "));
        var missing = await api.Add("priceList", """{"knowledgeBase":"offer"}""");

        foreach (var response in new[] { blank, missing })
        {
            var mistakes = await FactApi.MistakesOf(response);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal(
                ["article.content.missing", "article.description.missing", "article.llmGuide.missing", "article.title.missing"],
                Codes(mistakes)
            );
            Assert.All(mistakes, mistake => Assert.False(string.IsNullOrWhiteSpace(mistake.GetProperty("fix").GetString())));
        }
        Assert.Empty(await api.List());
    }

    [Theory]
    [Trait("criterion", "AC-articles-3")]
    [InlineData("Price list")]
    [InlineData("price_list")]
    public async Task An_id_that_is_not_camelCase_is_refused_with_the_id_to_use(string id)
    {
        var api = await NewBot();

        var response = await api.Add(id, ArticleApi.Body());
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("article.id.invalid", mistake.GetProperty("code").GetString());
        Assert.Contains("Rename the article to 'priceList'", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal);
        Assert.Empty(await api.List());
    }

    [Fact]
    [Trait("criterion", "AC-articles-3")]
    public async Task An_id_another_article_of_the_bot_has_is_refused_even_when_the_article_is_in_another_base()
    {
        var api = await NewBot();
        await api.Add("priceList", ArticleApi.Body(title: "First"));

        var response = await api.Add("priceList", ArticleApi.Body("forValuers", title: "Second"));
        var mistake = Assert.Single(await FactApi.MistakesOf(response));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("resource.exists", mistake.GetProperty("code").GetString());
        Assert.Contains("edit the existing article", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal);
        var kept = Assert.Single(await api.List());
        Assert.Equal(("First", "offer"), (kept.Title, kept.KnowledgeBase));
    }

    [Fact]
    [Trait("criterion", "AC-articles-3")]
    public async Task A_knowledge_base_the_bot_does_not_have_is_refused_listing_the_bases_it_has_on_an_add_and_on_a_move()
    {
        var api = await NewBot();
        await api.Add("priceList", ArticleApi.Body());

        var add = await api.Add("other", ArticleApi.Body("nowhere"));
        var move = await api.Put("priceList", ArticleApi.Body("nowhere"));

        foreach (var response in new[] { add, move })
        {
            var mistake = Assert.Single(await FactApi.MistakesOf(response));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Equal("article.knowledgeBase.unknown", mistake.GetProperty("code").GetString());
            Assert.Contains("forValuers, offer", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal);
        }
        Assert.Equal("offer", Assert.Single(await api.List()).KnowledgeBase);
    }

    [Fact]
    [Trait("criterion", "AC-articles-4")]
    public async Task Content_and_guide_come_back_byte_for_byte_with_markdown_non_ascii_letters_and_trailing_newlines()
    {
        var api = await NewBot();
        const string content =
            "# Cennik\r\n\r\n- zażółć gęślą jaźń — \U0001F600\n- `code` and \"quotes\" and \\ backslash\t tab\n  indented\n\n\n";
        const string guide = "  Leading spaces, **bold**, <b>html stays</b>, a & b\n\n";

        await api.Add("priceList", ArticleApi.Body(content: content, llmGuide: guide));

        var shown = await ArticleApi.ViewOf(await api.Get("priceList"));
        Assert.Equal(content, shown.Content);
        Assert.Equal(guide, shown.LlmGuide);
        var listed = Assert.Single(await api.List());
        Assert.Equal(content, listed.Content);
        Assert.Equal(guide, listed.LlmGuide);
    }

    [Fact]
    [Trait("criterion", "AC-articles-6")]
    public async Task An_edit_replaces_the_fields_and_raises_the_revision_and_a_new_knowledgeBase_moves_the_article()
    {
        var api = await NewBot();
        await api.Add("priceList", ArticleApi.Body());

        var response = await api.Put(
            "priceList",
            ArticleApi.Body("forValuers", title: "New title", content: "New content"),
            "?ifRevision=1"
        );

        var saved = await ArticleApi.ViewOf(response);
        Assert.Equal(("forValuers", "New title", "New content", 2), (saved.KnowledgeBase, saved.Title, saved.Content, saved.Revision));
        Assert.Empty(await api.List("?knowledgeBase=offer"));
        Assert.Equal(["priceList"], (await api.List("?knowledgeBase=forValuers")).Select(article => article.Id));
        Assert.Equal(ArticleApi.Describe(saved), ArticleApi.Describe(await ArticleApi.ViewOf(await api.Get("priceList"))));
    }

    [Fact]
    [Trait("criterion", "AC-articles-7")]
    public async Task An_edit_or_delete_based_on_an_old_revision_is_refused_with_the_current_revision_and_changes_nothing()
    {
        var api = await NewBot();
        await api.Add("priceList", ArticleApi.Body(title: "First"));
        await api.Put("priceList", ArticleApi.Body(title: "Second"), "?ifRevision=1");

        var edit = await api.Put("priceList", ArticleApi.Body(title: "From a stale screen"), "?ifRevision=1");
        var delete = await api.Delete("priceList", "?ifRevision=1");

        foreach (var response in new[] { edit, delete })
        {
            var mistake = Assert.Single(await FactApi.MistakesOf(response));
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal("resource.stale", mistake.GetProperty("code").GetString());
            Assert.Contains("revision 2", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        }
        var kept = Assert.Single(await api.List());
        Assert.Equal(("Second", 2), (kept.Title, kept.Revision));
    }

    [Fact]
    [Trait("criterion", "AC-articles-7")]
    public async Task A_dry_run_of_an_add_an_edit_and_a_delete_answers_as_the_real_one_and_changes_nothing()
    {
        var api = await NewBot();

        var dryAdd = await api.Add("priceList", ArticleApi.Body(), "&dryRun=true");
        Assert.Empty(await api.List());
        var realAdd = await api.Add("priceList", ArticleApi.Body());
        var dryEdit = await api.Put("priceList", ArticleApi.Body(title: "After"), "?dryRun=true&ifRevision=1");
        var dryDelete = await api.Delete("priceList", "?dryRun=true");
        var refused = await api.Put("priceList", ArticleApi.Body(title: " "), "?dryRun=true");

        Assert.Equal(ArticleApi.Describe(await ArticleApi.ViewOf(realAdd)), ArticleApi.Describe(await ArticleApi.ViewOf(dryAdd)));
        var edited = await ArticleApi.ViewOf(dryEdit);
        Assert.Equal(("After", 2), (edited.Title, edited.Revision));
        Assert.Equal(HttpStatusCode.NoContent, dryDelete.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        var kept = Assert.Single(await api.List());
        Assert.Equal(("Price list", 1), (kept.Title, kept.Revision));
    }

    [Fact]
    [Trait("criterion", "AC-articles-8")]
    public async Task An_article_is_deleted_and_is_gone_from_the_list_and_from_its_address()
    {
        var api = await NewBot();
        await api.Add("priceList", ArticleApi.Body());
        await api.Add("other", ArticleApi.Body());

        var response = await api.Delete("priceList");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["other"], (await api.List()).Select(article => article.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await api.Get("priceList")).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-articles-11")]
    public async Task The_list_filters_by_base_and_by_text_in_the_title_id_or_description_ignoring_case_and_never_in_the_content()
    {
        var api = await NewBot();
        await api.Add("discounts", ArticleApi.Body(title: "Prices", description: "Whether there is a RABAT for regulars"));
        await api.Add("rabatPolicy", ArticleApi.Body(title: "Policy", description: "Rules"));
        await api.Add("titled", ArticleApi.Body(title: "Rabat for students", description: "Students"));
        await api.Add("inContent", ArticleApi.Body(title: "Other", content: "A rabat is a discount.", description: "Unrelated"));
        await api.Add("elsewhere", ArticleApi.Body("forValuers", title: "Rabat in another base", description: "Rabat"));

        Assert.Equal(["discounts", "elsewhere", "rabatPolicy", "titled"], (await api.List("?text=rabat")).Select(article => article.Id));
        Assert.Equal(
            ["discounts", "rabatPolicy", "titled"],
            (await api.List("?knowledgeBase=offer&text=RaBaT")).Select(article => article.Id)
        );
        Assert.Equal(["elsewhere"], (await api.List("?knowledgeBase=forValuers")).Select(article => article.Id));
        Assert.Equal(5, (await api.List("?text=")).Count);
        Assert.Empty(await api.List("?text=nothing%20like%20it"));
    }

    [Fact]
    [Trait("criterion", "AC-articles-12")]
    public async Task A_content_or_guide_over_the_limit_is_refused_with_the_limit_and_the_length_and_one_at_the_limit_is_kept()
    {
        var api = await NewBot();
        var atLimit = new string('x', 200_000);

        var accepted = await api.Add("atLimit", ArticleApi.Body(content: atLimit, llmGuide: atLimit));
        var refused = await api.Add("tooLong", ArticleApi.Body(content: atLimit + "x", llmGuide: atLimit + "yy"));
        var mistakes = await FactApi.MistakesOf(refused);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal(["article.content.tooLong", "article.llmGuide.tooLong"], Codes(mistakes));
        var content = mistakes.Single(mistake => mistake.GetProperty("path").GetString() == "content");
        Assert.Contains("has 200001 characters; the limit is 200000", content.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(["atLimit"], (await api.List()).Select(article => article.Id));
    }

    [Fact]
    [Trait("criterion", "AC-articles-15")]
    public async Task An_article_of_one_bot_is_not_visible_from_another_bot_and_two_bots_can_share_an_article_id()
    {
        var first = await NewBot();
        var second = await ArticleApi.NewBotAsync(app.CreateClient());
        await first.Add("priceList", ArticleApi.Body(title: "Of the first"));
        await first.Add("onlyHere", ArticleApi.Body());

        Assert.Equal(HttpStatusCode.OK, (await second.Add("priceList", ArticleApi.Body(title: "Of the second"))).StatusCode);

        Assert.Equal(["priceList"], (await second.List()).Select(article => article.Id));
        Assert.Equal("Of the second", Assert.Single(await second.List()).Title);
        Assert.Equal(HttpStatusCode.NotFound, (await second.Get("onlyHere")).StatusCode);
        Assert.Equal(["onlyHere", "priceList"], (await first.List()).Select(article => article.Id));
    }
}
