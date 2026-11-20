using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Suwagcms.Contracts.Availability;
using Suwagcms.Sdk.BotRuntime;
using Suwagcms.Sdk.BotRuntime.Availability;
using Suwagcms.Sdk.BotRuntime.Versions;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Server.Tests.Articles;
using Suwagcms.Server.Tests.Versions;

namespace Suwagcms.Server.Tests.Availability;

internal sealed class AvailabilityScene(PushScene push, TestBot bot, ArticleApi articles) : IAsyncDisposable
{
    public const string Used = "cheaper";
    public const string Referrer = "priceList";
    public const string Unused = "unused";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public PushScene Push { get; } = push;

    public TestBot Bot { get; } = bot;

    public ArticleApi Articles { get; } = articles;

    public string Id => Push.Bot;

    public static async Task<AvailabilityScene> StartAsync(HttpClient client, IVersionStore? store = null, BotCapabilities? declares = null)
    {
        var bot = await TestBot.StartAsync(PushScene.FreshId(), store, declares: declares);
        var push = await PushScene.RegisterAsync(client, bot.Id, bot.Address);
        await push.FillAsync();
        var articles = new ArticleApi(client, bot.Id);
        Assert.True((await articles.Add(Unused, ArticleApi.Body(title: "Unused", content: "Nothing links here."))).IsSuccessStatusCode);
        return new AvailabilityScene(push, bot, articles);
    }

    public async Task<HttpResponseMessage> TurnRaw(string kind, string id, string word, string query = "") =>
        await Push.Client.PostAsync(
            $"/api/bots/{Id}/availability/{kind}/{Uri.EscapeDataString(id)}/{word}{query}",
            null,
            TestContext.Current.CancellationToken
        );

    public async Task<AvailabilityAnswer> Turn(string word, string id = Used, string query = "", string kind = "article")
    {
        var response = await TurnRaw(kind, id, word, query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<AvailabilityAnswer>(Web, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("no answer");
    }

    public async Task<AvailabilityView> View(bool ask = true) =>
        await Push.Client.GetFromJsonAsync<AvailabilityView>(
            $"/api/bots/{Id}/availability?ask={(ask ? "true" : "false")}",
            Web,
            TestContext.Current.CancellationToken
        ) ?? throw new InvalidOperationException("no view");

    public async Task<bool> IsOffInCms(string id = Used) => (await View(ask: false)).Resources.Single(item => item.Id == id).Off;

    public async Task<AvailabilityDelivery> Send()
    {
        var response = await Push.Client.PostAsync($"/api/bots/{Id}/availability/send", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<AvailabilityDelivery>(Web, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("no delivery");
    }

    public async Task<BotAvailability> OnBot() =>
        JsonSerializer.Deserialize<BotState>(await TestBot.StateAsync(Bot.Address), BotJson)?.Availability
        ?? throw new InvalidOperationException("no state");

    private static readonly JsonSerializerOptions BotJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public async ValueTask DisposeAsync() => await Bot.DisposeAsync();
}
