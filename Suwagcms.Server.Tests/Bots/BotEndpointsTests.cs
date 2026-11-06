using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Suwagcms.Contracts.Bots;
using Suwagcms.Contracts.Facts;

namespace Suwagcms.Server.Tests.Bots;

public sealed class BotEndpointsTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    private static string FreshId() => "t" + Guid.NewGuid().ToString("N")[..12];

    private HttpClient Client()
    {
        _ = database.Pool;
        return app.CreateClient();
    }

    private static Task<HttpResponseMessage> Register(
        HttpClient client,
        string id,
        string name = "A bot",
        string address = "http://bot.internal:8080"
    ) => client.PostAsJsonAsync("/api/bots", new RegisterBot(id, name, address), TestContext.Current.CancellationToken);

    private static async Task<List<Bot>> Listed(HttpClient client) =>
        await client.GetFromJsonAsync<List<Bot>>("/api/bots", TestContext.Current.CancellationToken) ?? [];

    private static async Task<List<JsonElement>> MistakesOf(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        return [.. document.RootElement.GetProperty("mistakes").EnumerateArray().Select(mistake => mistake.Clone())];
    }

    private static string[] Codes(IEnumerable<JsonElement> mistakes) =>
        [.. mistakes.Select(mistake => mistake.GetProperty("code").GetString() ?? "").Order(StringComparer.Ordinal)];

    [Fact]
    [Trait("criterion", "AC-bot-profiles-1")]
    public async Task A_registered_bot_appears_in_the_list_with_its_id_name_and_address()
    {
        using var client = Client();
        var id = FreshId();

        var response = await Register(client, id, "Rzeczoznawca", "http://rzeczoznawca.internal:8080");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains(new Bot(id, "Rzeczoznawca", "http://rzeczoznawca.internal:8080"), await Listed(client));
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-4")]
    public async Task A_registration_with_every_field_wrong_is_refused_with_every_mistake_and_its_fix_and_registers_nothing()
    {
        using var client = Client();

        var response = await Register(client, "Not Valid", " ", "rzeczoznawca.internal");
        var mistakes = await MistakesOf(response);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["bot.addressInvalid", "bot.idInvalid", "bot.nameMissing"], Codes(mistakes));
        Assert.All(mistakes, mistake => Assert.False(string.IsNullOrWhiteSpace(mistake.GetProperty("fix").GetString())));
        // Why: other test classes register bots at the same time, so the whole list cannot be compared before and after.
        Assert.DoesNotContain(await Listed(client), bot => bot.Id == "Not Valid" || bot.Address == "rzeczoznawca.internal");
    }

    [Theory]
    [Trait("criterion", "AC-bot-profiles-4")]
    [InlineData("", "bot.idInvalid")]
    [InlineData("1abc", "bot.idInvalid")]
    [InlineData("Abc", "bot.idInvalid")]
    [InlineData("a_b", "bot.idInvalid")]
    [InlineData("a b", "bot.idInvalid")]
    public async Task An_id_that_is_missing_or_does_not_follow_the_rule_is_refused(string id, string code)
    {
        using var client = Client();

        var response = await Register(client, id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal([code], Codes(await MistakesOf(response)));
    }

    [Theory]
    [Trait("criterion", "AC-bot-profiles-4")]
    [InlineData("ftp://bot.internal")]
    [InlineData("/relative/path")]
    [InlineData("")]
    [InlineData("http://")]
    public async Task An_address_that_is_not_an_absolute_http_or_https_address_is_refused(string address)
    {
        using var client = Client();

        var response = await Register(client, FreshId(), address: address);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["bot.addressInvalid"], Codes(await MistakesOf(response)));
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-4")]
    public async Task A_body_with_no_fields_is_refused_with_all_three_mistakes()
    {
        using var client = Client();
        using var empty = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/bots", empty, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(["bot.addressInvalid", "bot.idInvalid", "bot.nameMissing"], Codes(await MistakesOf(response)));
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-5")]
    public async Task An_id_that_is_taken_is_refused_with_a_mistake_naming_the_id_and_keeps_the_first_bot()
    {
        using var client = Client();
        var id = FreshId();
        await Register(client, id, "First");

        var response = await Register(client, id, "Second");
        var mistake = Assert.Single(await MistakesOf(response));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("bot.idTaken", mistake.GetProperty("code").GetString());
        Assert.Contains(id, mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal("First", Assert.Single(await Listed(client), bot => bot.Id == id).Name);
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-5")]
    public async Task Two_registrations_of_one_id_at_the_same_moment_let_one_succeed_and_refuse_the_other()
    {
        using var client = Client();
        var id = FreshId();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Register(client, id)));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(3, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        Assert.Single(await Listed(client), bot => bot.Id == id);
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-6")]
    public async Task Registering_a_bot_whose_address_cannot_be_reached_succeeds()
    {
        using var client = Client();
        var id = FreshId();

        var response = await Register(client, id, address: "http://127.0.0.1:1");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains(await Listed(client), bot => bot.Id == id);
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-6")]
    public async Task Registering_a_bot_sends_no_request_to_its_address()
    {
        using var client = Client();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var response = await Register(client, FreshId(), address: $"http://127.0.0.1:{port}");
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(listener.Pending(), "something connected to the bot's address while registering");
    }
}
