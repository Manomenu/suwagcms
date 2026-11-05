using System.CommandLine;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Output;

namespace Suwagcms.Cli.Procedures;

// Why: a procedures command starts as a facts command does (output, address, bot), so it asks the same resolver and only adds the addresses of its own resource.
internal sealed record ProcedureTarget(BotTarget Inner)
{
    public BotCommandRun Run => Inner.Run;

    public Uri Address => Inner.Address;

    public string Bot => Inner.Bot;

    public CliEnvironment Machine => Inner.Machine;

    public string ProceduresPath => $"api/bots/{Uri.EscapeDataString(Bot)}/procedures";

    public string ProcedurePath(string id) => $"{ProceduresPath}/{Uri.EscapeDataString(id)}";

    public string LayoutPath(string id) => $"{ProcedurePath(id)}/layout";

    public Uri CreatorPage(string id) => new(Address, $"bots/{Uri.EscapeDataString(Bot)}/procedures/{Uri.EscapeDataString(id)}");

    public Uri Page => new(Address, $"bots/{Uri.EscapeDataString(Bot)}/procedures");

    public static async Task<(ProcedureTarget? Target, int ExitCode)> ResolveAsync(
        ParseResult parsed,
        GlobalOptions options,
        CliEnvironment environment,
        CancellationToken cancellationToken
    )
    {
        var (inner, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
        return (inner is null ? null : new ProcedureTarget(inner), exit);
    }

    public Task<int> OpenAsync(Uri page, ParseResult parsed) =>
        WebPage.ShowAsync(page, parsed.InvocationConfiguration.Output, parsed.InvocationConfiguration.Error, Machine.OpenBrowser);

    public Task<int> FailedAsync(ServerAnswer answer, CancellationToken cancellationToken) => Inner.FailedAsync(answer, cancellationToken);

    public Task<int> MisusedAsync(string text) => Run.Report.FailedAsync(ExitCodes.Misused, new FailureDocument(text), text);
}
