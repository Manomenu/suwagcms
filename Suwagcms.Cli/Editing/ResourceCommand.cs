using System.CommandLine;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;

namespace Suwagcms.Cli.Editing;

// Why: deleting works alike for every resource; only what is named and how it reads in --json differ.
internal static class ResourceCommand
{
    public static Command Delete<TDocument>(
        GlobalOptions options,
        CliEnvironment environment,
        string words,
        string collection,
        Argument<string> id,
        string description,
        Func<string, string, bool, TDocument> document
    )
    {
        var ifRevision = EditOptions.IfRevision();
        var dryRun = EditOptions.DryRun();
        var command = new Command("delete", description) { id, ifRevision, dryRun };
        EditOptions.RefuseOpen(command, options, words, ": what it deletes is gone");
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                var name = parsed.GetRequiredValue(id);
                var dry = parsed.GetValue(dryRun);
                var answer = await ServerCall.DeleteAsync(
                    environment,
                    target.Address,
                    target.ItemPath(collection, name) + EditOptions.Query(dry, parsed.GetValue(ifRevision)),
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                return await target.Run.Report.DoneAsync(
                    document(target.Bot, name, dry),
                    dry ? $"Would delete '{name}'. Nothing was changed." : $"Deleted '{name}'."
                );
            }
        );
        return command;
    }
}
