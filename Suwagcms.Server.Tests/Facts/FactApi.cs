using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Suwagcms.Contracts.Bots;
using Suwagcms.Contracts.Facts;

namespace Suwagcms.Server.Tests.Facts;

internal sealed class FactApi(HttpClient client, string bot)
{
    public string Bot { get; } = bot;

    public const string NoBotAddress = "http://bot.internal:8080";

    public static async Task<FactApi> NewBotAsync(HttpClient client, string address = NoBotAddress)
    {
        var id = "t" + Guid.NewGuid().ToString("N")[..12];
        var response = await client.PostAsJsonAsync(
            "/api/bots",
            new RegisterBot(id, "A bot", address),
            TestContext.Current.CancellationToken
        );
        response.EnsureSuccessStatusCode();
        return new FactApi(client, id);
    }

    public FactApi Another() => new(client, Bot);

    public async Task<HttpResponseMessage> Put(string name, string json, string query = "")
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PutAsync(
            $"/api/bots/{Bot}/facts/{Uri.EscapeDataString(name)}{query}",
            content,
            TestContext.Current.CancellationToken
        );
    }

    public Task<HttpResponseMessage> Add(string name, string json, string extra = "") => Put(name, json, "?ifRevision=0" + extra);

    public Task<HttpResponseMessage> Delete(string name, string query = "") =>
        client.DeleteAsync($"/api/bots/{Bot}/facts/{Uri.EscapeDataString(name)}{query}", TestContext.Current.CancellationToken);

    public Task<HttpResponseMessage> Get(string name) =>
        client.GetAsync($"/api/bots/{Bot}/facts/{Uri.EscapeDataString(name)}", TestContext.Current.CancellationToken);

    public async Task<List<Fact>> List() =>
        await client.GetFromJsonAsync<List<Fact>>($"/api/bots/{Bot}/facts", TestContext.Current.CancellationToken) ?? [];

    public static string Text(string description = "The city", int length = 80) =>
        $$"""{"type":"text","description":"{{description}}","maxLength":{{length}}}""";

    public const string Kind = """{"type":"enum","description":"What is valued","values":["house","flat"],"writableBy":"correct"}""";

    public const string Floors = """{"type":"int","description":"How many floors","min":1,"max":40,"writableBy":"assign"}""";

    public static async Task<List<JsonElement>> MistakesOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        return [.. document.RootElement.GetProperty("mistakes").EnumerateArray().Select(mistake => mistake.Clone())];
    }

    public static async Task<string[]> CodesOf(HttpResponseMessage response) =>
        [.. (await MistakesOf(response)).Select(mistake => mistake.GetProperty("code").GetString() ?? "").Order(StringComparer.Ordinal)];

    public static async Task<Fact> FactOf(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<Fact>(TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("no fact in the answer");

    public static string Describe(Fact fact) =>
        $"{fact.Name}|{fact.Type}|{fact.Description}|{fact.WritableBy}|{string.Join(',', fact.Values ?? [])}|{fact.Min}|{fact.Max}|{fact.MaxLength}|{fact.Revision}";
}
