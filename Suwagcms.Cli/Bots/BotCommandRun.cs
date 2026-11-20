using System.CommandLine;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Output;

namespace Suwagcms.Cli.Bots;

internal sealed record BotCommandRun(Report Report, Uri? Address, string Problem)
{
    public static BotCommandRun Start(ParseResult parsed, GlobalOptions options, CliEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(options);
        var report = new Report(parsed.InvocationConfiguration.Output, parsed.InvocationConfiguration.Error, parsed.GetValue(options.Json));
        var choice = ServerAddress.Choose(parsed.GetValue(options.Server), environment);
        return new BotCommandRun(report, choice.Address, choice.Problem ?? "The server address is unknown.");
    }

    public Task<int> BadAddressAsync() => Report.FailedAsync(ExitCodes.Misused, new FailureDocument(Problem), Problem);
}
