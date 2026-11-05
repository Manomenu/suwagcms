using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Suwagcms.Contracts;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.KnowledgeBases;

namespace Suwagcms.Server.Tests.Articles;

internal sealed class ArticleApi(HttpClient client, string bot)
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public string Bot { get; } = bot;

    public KnowledgeBaseApi KnowledgeBases { get; } = new(client, bot);

    // Why: most tests need a bot that already has the two bases offer and forValuers.
    public static async Task<ArticleApi> NewBotAsync(HttpClient client)
    {
        var api = new ArticleApi(client, (await FactApi.NewBotAsync(client)).Bot);
        (await api.KnowledgeBases.Add("offer", "Offer")).EnsureSuccessStatusCode();
        (await api.KnowledgeBases.Add("forValuers", "For valuers")).EnsureSuccessStatusCode();
        return api;
    }

    public async Task<HttpResponseMessage> Put(string id, string json, string query = "")
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PutAsync(
            $"/api/bots/{Bot}/articles/{Uri.EscapeDataString(id)}{query}",
            content,
            TestContext.Current.CancellationToken
        );
    }

    public Task<HttpResponseMessage> Add(string id, string json, string extra = "") => Put(id, json, "?ifRevision=0" + extra);

    public Task<HttpResponseMessage> Delete(string id, string query = "") =>
        client.DeleteAsync($"/api/bots/{Bot}/articles/{Uri.EscapeDataString(id)}{query}", TestContext.Current.CancellationToken);

    public Task<HttpResponseMessage> Get(string id) =>
        client.GetAsync($"/api/bots/{Bot}/articles/{Uri.EscapeDataString(id)}", TestContext.Current.CancellationToken);

    public async Task<List<ArticleView>> List(string query = "") =>
        await client.GetFromJsonAsync<List<ArticleView>>($"/api/bots/{Bot}/articles{query}", TestContext.Current.CancellationToken) ?? [];

    // Why: JSON text is built by the serializer, so a text with quotes, newlines or non-ASCII letters is sent as written.
    public static string Body(
        string knowledgeBase = "offer",
        string? language = null,
        string title = "Price list",
        string content = "What a valuation costs.",
        string llmGuide = "Ask which kind of property first.",
        string description = "What a valuation costs and what affects the price"
    ) => JsonSerializer.Serialize(new SaveArticle(knowledgeBase, language, title, content, llmGuide, description), Web);

    public static async Task<ArticleView> ViewOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<ArticleView>(TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("no article in the answer");

    public static string Describe(ArticleView view) =>
        $"{view.Id}|{view.KnowledgeBase}|{view.Language}|{view.Title}|{view.Content}|{view.LlmGuide}|{view.Description}|{view.Revision}";
}
