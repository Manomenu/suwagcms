using System.Net.Http.Json;
using System.Text;
using Suwagcms.Contracts.KnowledgeBases;
using Suwagcms.Server.Tests.Facts;

namespace Suwagcms.Server.Tests.KnowledgeBases;

internal sealed class KnowledgeBaseApi(HttpClient client, string bot)
{
    public string Bot { get; } = bot;

    public static async Task<KnowledgeBaseApi> NewBotAsync(HttpClient client) => new(client, (await FactApi.NewBotAsync(client)).Bot);

    public KnowledgeBaseApi InAnotherBot(string other) => new(client, other);

    public async Task<HttpResponseMessage> Put(string id, string json, string query = "")
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PutAsync(
            $"/api/bots/{Bot}/knowledge-bases/{Uri.EscapeDataString(id)}{query}",
            content,
            TestContext.Current.CancellationToken
        );
    }

    public Task<HttpResponseMessage> Add(string id, string title, string extra = "") => Put(id, Body(title), "?ifRevision=0" + extra);

    public Task<HttpResponseMessage> AddWithLanguage(string id, string title, string? language) =>
        Put(id, Body(title, language), "?ifRevision=0");

    public Task<HttpResponseMessage> Rename(string id, string title, string query = "") => Put(id, Body(title), query);

    public Task<HttpResponseMessage> Delete(string id, string query = "") =>
        client.DeleteAsync($"/api/bots/{Bot}/knowledge-bases/{Uri.EscapeDataString(id)}{query}", TestContext.Current.CancellationToken);

    public Task<HttpResponseMessage> Get(string id) =>
        client.GetAsync($"/api/bots/{Bot}/knowledge-bases/{Uri.EscapeDataString(id)}", TestContext.Current.CancellationToken);

    public async Task<List<KnowledgeBaseView>> List() =>
        await client.GetFromJsonAsync<List<KnowledgeBaseView>>($"/api/bots/{Bot}/knowledge-bases", TestContext.Current.CancellationToken)
        ?? [];

    // Why: a language left out is left out of the JSON too, so the server's default is what the test sees.
    public static string Body(string title, string? language = null) =>
        language is null ? $$"""{"title":"{{title}}"}""" : $$"""{"title":"{{title}}","language":"{{language}}"}""";

    public static async Task<KnowledgeBaseView> ViewOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<KnowledgeBaseView>(TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("no knowledge base in the answer");

    public static string Describe(KnowledgeBaseView view) => $"{view.Id}|{view.Title}|{view.Revision}";
}
