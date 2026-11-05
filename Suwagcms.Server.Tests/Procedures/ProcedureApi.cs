using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Suwagcms.Contracts;
using Suwagcms.Server.Tests.Facts;

namespace Suwagcms.Server.Tests.Procedures;

internal sealed class ProcedureApi(HttpClient client, FactApi facts)
{
    public FactApi Facts { get; } = facts;

    public string Bot => Facts.Bot;

    public static async Task<ProcedureApi> NewBotAsync(HttpClient client)
    {
        var facts = await FactApi.NewBotAsync(client);
        Assert.True((await facts.Add("customer.city", FactApi.Text())).IsSuccessStatusCode);
        Assert.True((await facts.Add("order.kind", FactApi.Kind)).IsSuccessStatusCode);
        Assert.True(
            (await facts.Add("order.floors", """{"type":"int","description":"How many floors","min":1,"max":40}""")).IsSuccessStatusCode
        );
        Assert.True(
            (
                await facts.Add("system.flag", """{"type":"text","description":"Set by a procedure","maxLength":5,"writableBy":"assign"}""")
            ).IsSuccessStatusCode
        );
        return new ProcedureApi(client, facts);
    }

    public async Task<HttpResponseMessage> Put(string id, string json, string query = "")
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PutAsync($"/api/bots/{Bot}/procedures/{id}{query}", content, TestContext.Current.CancellationToken);
    }

    public Task<HttpResponseMessage> Add(string id, string json, string extra = "") => Put(id, json, "?ifRevision=0" + extra);

    public Task<HttpResponseMessage> Delete(string id, string query = "") =>
        client.DeleteAsync($"/api/bots/{Bot}/procedures/{id}{query}", TestContext.Current.CancellationToken);

    public Task<HttpResponseMessage> Get(string id) =>
        client.GetAsync($"/api/bots/{Bot}/procedures/{id}", TestContext.Current.CancellationToken);

    public Task<HttpResponseMessage> GetLayout(string id) =>
        client.GetAsync($"/api/bots/{Bot}/procedures/{id}/layout", TestContext.Current.CancellationToken);

    public async Task<HttpResponseMessage> PutLayout(string id, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PutAsync($"/api/bots/{Bot}/procedures/{id}/layout", content, TestContext.Current.CancellationToken);
    }

    public async Task<ProcedureList> List() =>
        await client.GetFromJsonAsync<ProcedureList>($"/api/bots/{Bot}/procedures", TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("no list");

    public static async Task<ProcedureDetail> DetailOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<ProcedureDetail>(TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("no procedure in the answer");

    public static async Task<string[]> CodesOf(HttpResponseMessage response) =>
        [
            .. (await FactApi.MistakesOf(response))
                .Select(mistake => mistake.GetProperty("code").GetString() ?? "")
                .Order(StringComparer.Ordinal),
        ];

    public static string[] NotReadyCodes(ProcedureDetail detail) =>
        [.. detail.NotReady.Select(mistake => mistake.Code).Order(StringComparer.Ordinal)];

    public const string Conversation = """
        {"description":"The base of every conversation","firstStep":"wait",
         "steps":{"wait":{"listen":{},"next":"answer"},"answer":{"say":"Hello","next":"wait"}}}
        """;

    // Why: the example of the spec, with all four blocks and rule-based next.
    public const string Valuation = """
        {"description":"The customer orders a valuation of a property.","firstStep":"ask",
         "steps":{
           "ask":{"collect":["customer.city","order.kind"],"next":[
             {"when":{"order.kind":{"oneOf":["house"]}},"to":"mark"},
             {"to":"goodbye"}]},
           "mark":{"assign":{"system.flag":"big"},"next":"confirm"},
           "confirm":{"say":"Thank you, we will call you.","next":"ask"},
           "goodbye":{"say":"Sorry, we only value houses."}},
         "whenStalled":{"instruction":"Tell the customer you could not finish."}}
        """;

    public static string Normalised(string json) => JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement);
}
