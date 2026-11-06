using System.CommandLine;

namespace Suwagcms.Cli.Bots;

internal static class BotCommand
{
    // Why: a command on a bot's resources has nothing to do until it knows its output, address and bot, and failing to is answered the same way everywhere.
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
