using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Suwagcms.Sdk.BotRuntime;
using Suwagcms.Sdk.BotRuntime.Actions;
using Suwagcms.Sdk.BotRuntime.Availability;
using Suwagcms.Sdk.BotRuntime.Versions;

namespace Suwagcms.Server.Bots;

// Why: the one place the CMS talks to a bot's routes; it holds no lock and no transaction, since a slow bot must never hold up anyone's editing.
internal sealed class BotClient(HttpClient http, IOptions<PushOptions> options, TimeProvider time)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    internal abstract record Reply
    {
        internal sealed record Silent(string Reason) : Reply;

        internal sealed record Answer(int Status, string Body) : Reply;
    }

    public async Task<Reply> ReadStateAsync(string address, CancellationToken cancellationToken) =>
        await SendAsync(
            address,
            new HttpRequestMessage(HttpMethod.Get, Route(address, "state")),
            options.Value.StateTimeoutSeconds,
            cancellationToken
        );

    public async Task<Reply> ReadDeclarationAsync(string address, CancellationToken cancellationToken) =>
        await SendAsync(
            address,
            new HttpRequestMessage(HttpMethod.Get, Route(address, "declaration")),
            options.Value.StateTimeoutSeconds,
            cancellationToken
        );

    public async Task<Reply> PutVersionAsync(string address, int number, string document, CancellationToken cancellationToken) =>
        await SendAsync(
            address,
            new HttpRequestMessage(HttpMethod.Put, Route(address, $"versions/{number.ToString(CultureInfo.InvariantCulture)}"))
            {
                Content = new StringContent(document, Encoding.UTF8, "application/json"),
            },
            options.Value.PushTimeoutSeconds,
            cancellationToken
        );

    public async Task<Reply> PutCurrentAsync(string address, int number, string digest, CancellationToken cancellationToken) =>
        await SendAsync(
            address,
            new HttpRequestMessage(HttpMethod.Put, Route(address, "current"))
            {
                Content = JsonContent.Create(new CurrentChoice { Number = number, Digest = digest }, options: Json),
            },
            options.Value.PushTimeoutSeconds,
            cancellationToken
        );

    // Why: a person waits on the page for this, and the bot only writes it to its store, so it needs no more than reading the state does.
    public async Task<Reply> PutAvailabilityAsync(string address, BotAvailability availability, CancellationToken cancellationToken) =>
        await SendAsync(
            address,
            new HttpRequestMessage(HttpMethod.Put, Route(address, "availability"))
            {
                Content = new StringContent(JsonSerializer.Serialize(availability, Json), Encoding.UTF8, "application/json"),
            },
            options.Value.StateTimeoutSeconds,
            cancellationToken
        );

    public async Task<Reply> TestActionAsync(string address, string request, int seconds, CancellationToken cancellationToken) =>
        await SendAsync(
            address,
            new HttpRequestMessage(HttpMethod.Post, Route(address, "actions/test"))
            {
                Content = new StringContent(request, Encoding.UTF8, "application/json"),
            },
            seconds,
            cancellationToken
        );

    public static ActionOutcome? OutcomeIn(Reply.Answer answer) =>
        answer.Status is >= 200 and < 300
        && Parse<ActionOutcome>(answer.Body) is { Outcome: ActionOutcome.Success or ActionOutcome.Failure } outcome
            ? outcome
            : null;

    public static BotState? StateIn(Reply.Answer answer) => Parse<BotState>(answer.Body);

    public static BotRefusal? RefusalIn(Reply.Answer answer) => Parse<BotRefusal>(answer.Body);

    private static T? Parse<T>(string body)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Uri Route(string address, string path) => new($"{address.TrimEnd('/')}/suwagcms/{path}");

    private async Task<Reply> SendAsync(string address, HttpRequestMessage request, int seconds, CancellationToken cancellationToken)
    {
        using var message = request;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(seconds), time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            using var response = await http.SendAsync(message, linked.Token);
            return new Reply.Answer((int)response.StatusCode, await response.Content.ReadAsStringAsync(linked.Token));
        }
        catch (HttpRequestException)
        {
            return new Reply.Silent($"cannot reach {address}");
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new Reply.Silent(string.Create(CultureInfo.InvariantCulture, $"{address} did not answer within {seconds} seconds"));
        }
    }
}
