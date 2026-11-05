using System.Net.Http.Json;
using Suwagcms.Contracts;

namespace Suwagcms.Server.Tests.Usage;

internal static class UsageApi
{
    public static Task<HttpResponseMessage> Get(HttpClient client, string bot, string kind, string id) =>
        client.GetAsync($"/api/bots/{bot}/{kind}/{Uri.EscapeDataString(id)}/usage", TestContext.Current.CancellationToken);

    public static async Task<UsageView> Of(HttpClient client, string bot, string kind, string id) =>
        await (await Get(client, bot, kind, id)).Content.ReadFromJsonAsync<UsageView>(TestContext.Current.CancellationToken)
        ?? throw new InvalidOperationException("no usage in the answer");
}
