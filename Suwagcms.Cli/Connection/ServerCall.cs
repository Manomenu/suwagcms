using System.Net;
using System.Text.Json;

namespace Suwagcms.Cli.Connection;

internal static class ServerCall
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static async Task<ServerAnswer> GetAsync(
        CliEnvironment environment,
        Uri address,
        string path,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(environment);
        using var owned = new HttpClient { Timeout = Timeout };
        var http = environment.Http ?? owned;
        try
        {
            using var response = await http.GetAsync(new Uri(address, path), cancellationToken);
            return await AnswerAsync(address, response, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Unreachable(address);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unreachable(address);
        }
    }

    private static ServerAnswer Unreachable(Uri address) =>
        ServerAnswer.Failed(
            $"Cannot reach {ServerAddress.Display(address)}. Start the server, or pass --server or set {ServerAddress.Variable}."
        );

    private static async Task<ServerAnswer> AnswerAsync(Uri address, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return ServerAnswer.Done;
        }
        var body = await ReadJsonAsync(response, cancellationToken);
        if (
            (int)response.StatusCode is >= 400 and < 500
            && body is { } problem
            && problem.TryGetProperty("mistakes", out var mistakes)
            && mistakes.ValueKind == JsonValueKind.Array
            && mistakes.GetArrayLength() > 0
        )
        {
            return ServerAnswer.Refused([.. mistakes.EnumerateArray().Select(mistake => mistake.Clone())]);
        }
        var detail =
            body is { } found && found.TryGetProperty("detail", out var text) && text.ValueKind == JsonValueKind.String
                ? $" {text.GetString()}"
                : "";
        return ServerAnswer.Failed(
            $"{ServerAddress.Display(address)} answered {(int)response.StatusCode} {Reason(response.StatusCode)}.{detail} Check the server, or pass --server or set {ServerAddress.Variable}."
        );
    }

    private static string Reason(HttpStatusCode code) => code.ToString();

    private static async Task<JsonElement?> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
