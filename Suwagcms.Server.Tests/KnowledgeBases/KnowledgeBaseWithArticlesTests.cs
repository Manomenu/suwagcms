using System.Net;
using Suwagcms.Server.Tests.Articles;
using Suwagcms.Server.Tests.Facts;

namespace Suwagcms.Server.Tests.KnowledgeBases;

public sealed class KnowledgeBaseWithArticlesTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-9")]
    public async Task A_base_with_articles_cannot_be_deleted_and_the_refusal_lists_them_until_they_are_moved_or_deleted()
    {
        _ = database.Pool;
        var api = await ArticleApi.NewBotAsync(app.CreateClient());
        await api.Add("priceList", ArticleApi.Body());
        await api.Add("discounts", ArticleApi.Body());
        await api.Add("guide", ArticleApi.Body("forValuers"));

        var refused = await api.KnowledgeBases.Delete("offer");
        var mistakes = await FactApi.MistakesOf(refused);
        var dry = await api.KnowledgeBases.Delete("offer", "?dryRun=true");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, dry.StatusCode);
        Assert.Equal(
            ["discounts", "priceList"],
            mistakes.Select(mistake => mistake.GetProperty("resource").GetProperty("id").GetString()).Order(StringComparer.Ordinal)
        );
        Assert.All(
            mistakes,
            mistake =>
            {
                Assert.Equal("article.knowledgeBase.unknown", mistake.GetProperty("code").GetString());
                Assert.Contains("first move or delete its articles", mistake.GetProperty("fix").GetString(), StringComparison.Ordinal);
            }
        );
        Assert.Equal(["forValuers", "offer"], (await api.KnowledgeBases.List()).Select(item => item.Id));

        await api.Put("priceList", ArticleApi.Body("forValuers"));
        await api.Delete("discounts");

        Assert.Equal(HttpStatusCode.NoContent, (await api.KnowledgeBases.Delete("offer")).StatusCode);
        Assert.Equal(["forValuers"], (await api.KnowledgeBases.List()).Select(item => item.Id));
    }
}
