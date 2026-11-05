using System.CommandLine;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Contracts;

namespace Suwagcms.Cli.Versions;

internal static class PushCommand
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Command Create(GlobalOptions options, CliEnvironment environment)
    {
        var command = new Command(
            "push",
            "Push the latest version to the bot and report the version the bot says it has; asks for no confirmation and sends nothing when the bot already has it."
        );
        EditOptions.RefuseOpen(command, options, "push");
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                var answer = await ServerCall.GetAsync(
                    environment,
                    target.Address,
                    target.CollectionPath("versions/status"),
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var status = answer.Body?.Deserialize<BotStatus>(Web) ?? throw new InvalidOperationException("the server sent no status");
                if (status.Latest is not { } latest)
                {
                    const string none = "No version to push. Create one: suwagcms versions create";
                    return await target.Run.Report.FailedWithAsync(
                        ExitCodes.Refused,
                        new PushDocument(target.Bot, PushDocument.Failed, 0, status.Has, none, []),
                        none
                    );
                }
                if (status.Reach == BotReach.Answered && status.Has == latest)
                {
                    return await target.Run.Report.DoneAsync(
                        new PushDocument(target.Bot, PushDocument.Unchanged, latest, latest, null, []),
                        VersionText.AlreadyPushed(target.Bot, latest)
                    );
                }
                var pushed = await ServerCall.PostAsync(
                    environment,
                    target.Address,
                    $"{target.ItemPath("versions", latest.ToString(System.Globalization.CultureInfo.InvariantCulture))}/push",
                    new { },
                    cancellationToken
                );
                if (pushed.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(pushed, cancellationToken);
                }
                var result =
                    pushed.Body?.Deserialize<PushResult>(Web) ?? throw new InvalidOperationException("the server sent no push result");
                var document = PushDocument.Of(target.Bot, result);
                return result.Outcome == PushOutcome.Succeeded
                    ? await target.Run.Report.DoneAsync(document, VersionText.Pushed(target.Bot, result))
                    : await target.Run.Report.FailedWithAsync(ExitCodes.Refused, document, VersionText.PushFailed(result));
            }
        );
        return command;
    }
}
