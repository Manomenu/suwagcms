using System.CommandLine;

namespace Suwagcms.Cli.Bots;

internal static class BotCommand
{
    public static void SetBotAction(
        this Command command,
        GlobalOptions options,
        CliEnvironment environment,
        Func<BotTarget, ParseResult, CancellationToken, Task<int>> action
    ) =>
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                return target is null ? exit : await action(target, parsed, cancellationToken);
            }
        );
}
