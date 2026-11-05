using System.CommandLine;
using System.Text.Json;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Output;

namespace Suwagcms.Cli.Bots;

// Why: every command on a bot's resources starts the same way: the output asked for, the server address, and the bot, which a resource belongs to.
internal sealed record BotTarget(BotCommandRun Run, Uri Address, string Bot, CliEnvironment Machine)
{
    public string CollectionPath(string collection) => $"api/bots/{Uri.EscapeDataString(Bot)}/{collection}";

    public string ItemPath(string collection, string id) => $"{CollectionPath(collection)}/{Uri.EscapeDataString(id)}";

    // Why: the web app opens a fact or an article in its list from ?open=<id>, and a procedure in its creator.
    private string? LinkTo(string kind, string id) =>
        kind switch
        {
            "GlobalFact" => new Uri(PageOf("facts"), $"?open={Uri.EscapeDataString(id)}").AbsoluteUri,
            "Article" => new Uri(PageOf("articles"), $"?open={Uri.EscapeDataString(id)}").AbsoluteUri,
            "Action" => new Uri(PageOf("actions"), $"?open={Uri.EscapeDataString(id)}").AbsoluteUri,
            "Persona" => PageOf("persona").AbsoluteUri,
            "Procedure" => new Uri(Address, $"bots/{Uri.EscapeDataString(Bot)}/procedures/{Uri.EscapeDataString(id)}").AbsoluteUri,
            _ => null,
        };

    // Why: a base is shown in its list, which opens no single base from the address.
    public Uri PageAt(string kind, string id) => LinkTo(kind, id) is { } link ? new Uri(link) : PageOf("knowledge-bases");

    public Uri PageOf(string collection) => new(Address, $"bots/{Uri.EscapeDataString(Bot)}/{collection}");

    public static async Task<(BotTarget? Target, int ExitCode)> ResolveAsync(
        ParseResult parsed,
        GlobalOptions options,
        CliEnvironment environment,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(options);
        var run = BotCommandRun.Start(parsed, options, environment);
        if (run.Address is not { } address)
        {
            return (null, await run.BadAddressAsync());
        }
        if (ChosenBot.Choose(parsed.GetValue(options.Bot), environment) is not { } chosen)
        {
            var text =
                $"No bot is chosen. Choose one with --bot <id>, the {ChosenBot.Variable} variable or `suwagcms use <id>`. {await BotsLineAsync(environment, address, cancellationToken)}";
            return (null, await run.Report.FailedAsync(ExitCodes.Misused, new FailureDocument(text), text));
        }
        return (new BotTarget(run, address, chosen.Id, environment), ExitCodes.Done);
    }

    // Why: a refusal for an unknown bot says which bots exist, as `suwagcms use` does, so the next command is right.
    public async Task<int> FailedAsync(ServerAnswer answer, CancellationToken cancellationToken)
    {
        var unknownBot = answer.Mistakes.Any(mistake =>
            mistake.ValueKind == JsonValueKind.Object
            && mistake.TryGetProperty("code", out var code)
            && code.ValueKind == JsonValueKind.String
            && code.GetString() == "bot.notFound"
        );
        var shown = unknownBot
            ? answer with
            {
                Message = $"{answer.Message}{Environment.NewLine}{await BotsLineAsync(Machine, Address, cancellationToken)}",
            }
            : answer with
            {
                // Why: a command may put its own heading above the mistakes, so only their lines get the links.
                Message =
                    answer.Mistakes.Count == 0
                        ? answer.Message
                        : answer.Message.Replace(
                            ServerAnswer.Describe(answer.Mistakes, null),
                            ServerAnswer.Describe(answer.Mistakes, LinkTo),
                            StringComparison.Ordinal
                        ),
            };
        return await Run.Report.ReportAsync(shown, Address);
    }

    private static async Task<string> BotsLineAsync(CliEnvironment environment, Uri address, CancellationToken cancellationToken)
    {
        var (bots, _) = await BotListing.FetchAsync(environment, address, cancellationToken);
        return bots is null ? "" : $"Bots: {BotListing.Names(bots)}.";
    }
}
