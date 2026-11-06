using System.CommandLine;
using System.Globalization;
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
        var version = new Option<int?>("--version")
        {
            Description = "Push this kept version instead of the latest; that is the rollback, and it changes only the bot.",
        };
        var dryRun = new Option<bool>("--dry-run")
        {
            Description =
                "Say whether the version would be sent, made current or needs nothing, and what it changes; the bot stays as it is.",
        };
        var command = new Command(
            "push",
            "Push the latest version, or the one --version names, to the bot and report the version the bot says it has; asks for no confirmation and sends nothing when it is already current."
        )
        {
            version,
            dryRun,
        };
        EditOptions.RefuseOpen(command, options, "push");
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
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
                var number = parsed.GetValue(version) ?? latest;
                var path = target.ItemPath("versions", number.ToString(CultureInfo.InvariantCulture));
                if (parsed.GetValue(dryRun))
                {
                    return await PlanAsync(target, environment, path, cancellationToken);
                }
                if (status.Reach == BotReach.Answered && status.Has == number && !status.HasUnknownVersion)
                {
                    return await target.Run.Report.DoneAsync(
                        new PushDocument(target.Bot, PushDocument.Unchanged, number, number, null, []),
                        VersionText.AlreadyPushed(target.Bot, number)
                    );
                }
                var pushed = await ServerCall.PostAsync(environment, target.Address, $"{path}/push", new { }, cancellationToken);
                if (pushed.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(pushed, cancellationToken);
                }
                var result =
                    pushed.Body?.Deserialize<PushResult>(Web) ?? throw new InvalidOperationException("the server sent no push result");
                var document = PushDocument.Of(target.Bot, result);
                return result.Outcome == PushOutcome.Succeeded
                    ? await target.Run.Report.DoneAsync(document, VersionText.Pushed(target.Bot, result, latest))
                    : await target.Run.Report.FailedWithAsync(ExitCodes.Refused, document, VersionText.PushFailed(result));
            }
        );
        return command;
    }

    private static async Task<int> PlanAsync(BotTarget target, CliEnvironment environment, string path, CancellationToken cancellationToken)
    {
        var answer = await ServerCall.GetAsync(environment, target.Address, $"{path}/push-plan", cancellationToken);
        if (answer.ExitCode != ExitCodes.Done)
        {
            return await target.FailedAsync(answer, cancellationToken);
        }
        var plan = answer.Body?.Deserialize<PushPlan>(Web) ?? throw new InvalidOperationException("the server sent no plan");
        return plan.Problem is null
            ? await target.Run.Report.DoneAsync(plan, VersionText.Planned(target.Bot, plan))
            : await target.Run.Report.FailedWithAsync(ExitCodes.Refused, plan, VersionText.PlanFailed(plan));
    }
}
