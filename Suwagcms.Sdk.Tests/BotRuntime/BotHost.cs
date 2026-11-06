using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Suwagcms.Sdk.BotRuntime;
using Suwagcms.Sdk.BotRuntime.Versions;
using Suwagcms.Sdk.Format.Declaration;

namespace Suwagcms.Sdk.Tests.BotRuntime;

internal sealed class BotHost : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly WebApplication app;

    private BotHost(WebApplication app, HttpClient client)
    {
        this.app = app;
        Client = client;
    }

    public HttpClient Client { get; }

    public IBotDefinitions Definitions => app.Services.GetRequiredService<IBotDefinitions>();

    public static async Task<BotHost> StartAsync(
        IVersionStore? store,
        string botId = "rzeczoznawca",
        TimeSpan? pollInterval = null,
        BotCapabilities? declares = null
    )
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSuwagcmsBot(botId, store, pollInterval ?? TimeSpan.FromHours(1), declares);
        var app = builder.Build();
        app.MapSuwagcmsBot();
        await app.StartAsync();
        var address = app.Urls.Single();
        return new BotHost(app, new HttpClient { BaseAddress = new Uri(address) });
    }

    public async Task<(HttpStatusCode Status, JsonNode Body)> PutAsync(int number, string document)
    {
        using var content = new StringContent(document, System.Text.Encoding.UTF8, "application/json");
        using var response = await Client.PutAsync(new Uri($"/suwagcms/versions/{number}", UriKind.Relative), content);
        return (
            response.StatusCode,
            JsonNode.Parse(await response.Content.ReadAsStringAsync()) ?? throw new InvalidOperationException("empty answer")
        );
    }

    public Task<(HttpStatusCode Status, JsonNode Body)> PutAsync(int number, JsonObject document) =>
        PutAsync(number, document.ToJsonString());

    public async Task<(HttpStatusCode Status, JsonNode Body)> MakeCurrentAsync(int number, string digest)
    {
        using var content = new StringContent(
            new JsonObject { ["number"] = number, ["digest"] = digest }.ToJsonString(),
            System.Text.Encoding.UTF8,
            "application/json"
        );
        using var response = await Client.PutAsync(new Uri("/suwagcms/current", UriKind.Relative), content);
        return (
            response.StatusCode,
            JsonNode.Parse(await response.Content.ReadAsStringAsync()) ?? throw new InvalidOperationException("empty answer")
        );
    }

    public async Task<BotState> StateAsync() =>
        await Client.GetFromJsonAsync<BotState>(new Uri("/suwagcms/state", UriKind.Relative), Web)
        ?? throw new InvalidOperationException("empty state");

    public static string Describe(BotState state) =>
        $"{state.Bot}|{state.Current}|{string.Join(',', state.Held)}|{string.Join(',', state.InUse)}|{state.StoreError}";

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await app.StopAsync();
        await app.DisposeAsync();
    }
}
