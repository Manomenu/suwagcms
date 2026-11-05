using System.Text;
using System.Text.Json;
using Suwagcms.Server.Tests.Facts;

namespace Suwagcms.Server.Tests.Actions;

internal sealed class ActionApi(HttpClient client, string bot)
{
    public const string Full = """
        {"description":"Checks whether we serve an address.",
         "parameters":{"address":{"type":"text","maxLength":200,"description":"Street and city."}},
         "results":{"serviceable":{"type":"enum","values":["yes","no"],"description":"Whether we serve it."}},
         "implementation":{"http":{"method":"POST","url":"https://crm.internal/serviceability","headers":{"Authorization":"Bearer {secret:CRM_TOKEN}"},"timeoutSeconds":20}}}
        """;

    public const string Least =
        """{"description":"Sends.","implementation":{"http":{"method":"POST","url":"https://crm.internal/send"}}}""";

    public string Bot { get; } = bot;

    public static async Task<ActionApi> NewBotAsync(HttpClient client) => new(client, (await FactApi.NewBotAsync(client)).Bot);

    public async Task<HttpResponseMessage> Put(string id, string json, string query = "")
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PutAsync($"/api/bots/{Bot}/actions/{id}{query}", content, TestContext.Current.CancellationToken);
    }

    public Task<HttpResponseMessage> Add(string id, string json, string extra = "") => Put(id, json, "?ifRevision=0" + extra);

    public Task<HttpResponseMessage> Delete(string id, string query = "") =>
        client.DeleteAsync($"/api/bots/{Bot}/actions/{id}{query}", TestContext.Current.CancellationToken);

    public Task<HttpResponseMessage> Get(string id) =>
        client.GetAsync($"/api/bots/{Bot}/actions/{id}", TestContext.Current.CancellationToken);

    public async Task<JsonElement> Show(string id) => await Json(await Get(id));

    public async Task<JsonElement> List() =>
        await Json(await client.GetAsync($"/api/bots/{Bot}/actions", TestContext.Current.CancellationToken));

    public static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}
