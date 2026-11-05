using System.Net.Http.Json;
using Suwagcms.Contracts;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.Personas;

internal sealed class PersonaApi(HttpClient client, string bot)
{
    public const string Whole = """
        {"personality":"You help.\nBriefly.\n","scope":{"inScope":"Valuations\nGreetings","outOfScope":"Legal advice","whenOutOfScope":"Decline\nOffer help"}}
        """;

    public string Bot { get; } = bot;

    public static async Task<PersonaApi> NewBotAsync(HttpClient client) => new(client, (await VersionApi.NewBotAsync(client)).Bot);

    public async Task<HttpResponseMessage> Save(string body, string query = "")
    {
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        return await client.PutAsync(
            new Uri($"/api/bots/{Bot}/persona{query}", UriKind.Relative),
            content,
            TestContext.Current.CancellationToken
        );
    }

    public async Task<PersonaView> Read() =>
        await client.GetFromJsonAsync<PersonaView>($"/api/bots/{Bot}/persona", TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("no persona in the answer");

    public async Task<HistoryPage> HistoryOfPersona() =>
        await client.GetFromJsonAsync<HistoryPage>($"/api/bots/{Bot}/persona/persona/history", TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("no history in the answer");

    public async Task<HistoryPage> HistoryOfBot() =>
        await client.GetFromJsonAsync<HistoryPage>($"/api/bots/{Bot}/history", TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("no history in the answer");
}
