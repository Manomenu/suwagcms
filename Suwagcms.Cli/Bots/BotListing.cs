using System.Text.Json;
using Suwagcms.Cli.Connection;
using Suwagcms.Contracts;

namespace Suwagcms.Cli.Bots;

internal static class BotListing
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static async Task<(IReadOnlyList<Bot>? Bots, ServerAnswer Answer)> FetchAsync(
        CliEnvironment environment,
        Uri address,
        CancellationToken cancellationToken
    )
    {
        var answer = await ServerCall.GetAsync(environment, address, "api/bots", cancellationToken);
        return answer.ExitCode == ExitCodes.Done ? (answer.Body?.Deserialize<List<Bot>>(Web) ?? [], answer) : (null, answer);
    }

    public static string Names(IReadOnlyList<Bot> bots) =>
        bots.Count == 0 ? "none registered yet" : string.Join(", ", bots.Select(bot => bot.Id));
}
