using System.CommandLine;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Output;

namespace Suwagcms.Cli.Facts;

// Why: every facts command starts the same way: the output asked for, the server address, and the bot, which a fact belongs to.
internal sealed record FactTarget(BotCommandRun Run, Uri Address, string Bot, CliEnvironment Machine)
{
    public string FactsPath => $"api/bots/{Uri.EscapeDataString(Bot)}/facts";

    public string FactPath(string name) => $"{FactsPath}/{Uri.EscapeDataString(name)}";

    public Uri Page => new(Address, $"bots/{Uri.EscapeDataString(Bot)}/facts");

    public static async Task<(FactTarget? Target, int ExitCode)> ResolveAsync(
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
        return (new FactTarget(run, address, chosen.Id, environment), ExitCodes.Done);
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
            : answer;
        return await Run.Report.ReportAsync(shown, Address);
    }

    private static async Task<string> BotsLineAsync(CliEnvironment environment, Uri address, CancellationToken cancellationToken)
    {
        var (bots, _) = await BotListing.FetchAsync(environment, address, cancellationToken);
        return bots is null ? "" : $"Bots: {BotListing.Names(bots)}.";
    }
}
