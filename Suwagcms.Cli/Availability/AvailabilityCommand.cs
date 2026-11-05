using System.CommandLine;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Contracts;

namespace Suwagcms.Cli.Availability;

internal static class AvailabilityCommand
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Command Create(GlobalOptions options, CliEnvironment environment)
    {
        var command = new Command("availability", "Turn articles, procedures and actions on and off, a live setting outside versions.")
        {
            List(options, environment),
            Off(options, environment),
            On(options, environment),
            Send(options, environment),
        };
        return command;
    }

    private static Command List(GlobalOptions options, CliEnvironment environment)
    {
        var command = new Command(
            "list",
            "List every article, procedure and action, on or off, and say whether the bot has the setting. Changes nothing."
        );
        EditOptions.RefuseOpen(command, options, "availability list");
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
                    target.CollectionPath("availability"),
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var view =
                    answer.Body?.Deserialize<AvailabilityView>(Web)
                    ?? throw new InvalidOperationException("the server sent no availability");
                return await target.Run.Report.DoneAsync(
                    new AvailabilityListDocument(target.Bot, view.Resources, view.Delivery),
                    AvailabilityText.List(target.Bot, view)
                );
            }
        );
        return command;
    }

    private static Command Send(GlobalOptions options, CliEnvironment environment)
    {
        var command = new Command("send", "Send everything that is off to the bot again, and exit 1 unless the bot confirms it.");
        EditOptions.RefuseOpen(command, options, "availability send");
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                var answer = await ServerCall.PostAsync(
                    environment,
                    target.Address,
                    target.CollectionPath("availability/send"),
                    new { },
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var delivery =
                    answer.Body?.Deserialize<AvailabilityDelivery>(Web)
                    ?? throw new InvalidOperationException("the server sent no delivery");
                var document = new AvailabilitySendDocument(target.Bot, delivery);
                var text = AvailabilityText.Delivery(target.Bot, delivery);
                return delivery.State == AvailabilityDeliveryState.Confirmed
                    ? await target.Run.Report.DoneAsync(document, text)
                    : await target.Run.Report.FailedWithAsync(ExitCodes.Refused, document, text);
            }
        );
        return command;
    }

    private static Command Off(GlobalOptions options, CliEnvironment environment)
    {
        var yes = new Option<bool>("--yes") { Description = "Turn it off although something uses it; the list is printed either way." };
        return Turn(
            "off",
            "Turn a resource off, at once and on every version; a resource in use needs --yes, and no reference breaks.",
            yes,
            options,
            environment
        );
    }

    private static Command On(GlobalOptions options, CliEnvironment environment) =>
        Turn("on", "Turn a resource on, at once and with no question.", null, options, environment);

    private static Command Turn(string word, string description, Option<bool>? yes, GlobalOptions options, CliEnvironment environment)
    {
        var kind = new Argument<string>("kind") { Description = "article, procedure or action." };
        var id = new Argument<string>("id") { Description = "The resource's id." };
        var dryRun = EditOptions.DryRun();
        var command = new Command(word, description) { kind, id, dryRun };
        if (yes is not null)
        {
            command.Add(yes);
        }
        EditOptions.RefuseOpen(command, options, $"availability {word}");
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                var path = target.CollectionPath(
                    $"availability/{Uri.EscapeDataString(parsed.GetRequiredValue(kind))}/{Uri.EscapeDataString(parsed.GetRequiredValue(id))}/{word}"
                );
                var dry = parsed.GetValue(dryRun);
                var confirmed = yes is not null && parsed.GetValue(yes);
                // Why: asking first is what puts the list before the change, and the server's dry run is the one source of the list and of every refusal.
                if (dry || (yes is not null && !confirmed))
                {
                    var (preview, previewFailure) = await CallAsync(target, path + "?dryRun=true", environment, cancellationToken);
                    if (preview is null)
                    {
                        return previewFailure;
                    }
                    if (dry)
                    {
                        return await target.Run.Report.DoneAsync(Document(target.Bot, preview, true), AvailabilityText.DryRun(preview));
                    }
                    if (preview.Changed && preview.UsedBy.Count > 0)
                    {
                        return await target.Run.Report.FailedWithAsync(
                            ExitCodes.Refused,
                            Document(target.Bot, preview, true),
                            AvailabilityText.NeedsYes(preview)
                        );
                    }
                }
                var (done, failure) = await CallAsync(target, path, environment, cancellationToken);
                if (done is null)
                {
                    return failure;
                }
                var text = AvailabilityText.Done(target.Bot, done);
                return done.Delivery.State == AvailabilityDeliveryState.Confirmed
                    ? await target.Run.Report.DoneAsync(Document(target.Bot, done, false), text)
                    : await target.Run.Report.FailedWithAsync(ExitCodes.Refused, Document(target.Bot, done, false), text);
            }
        );
        return command;
    }

    private static async Task<(AvailabilityAnswer? Answer, int ExitCode)> CallAsync(
        BotTarget target,
        string path,
        CliEnvironment environment,
        CancellationToken cancellationToken
    )
    {
        var answer = await ServerCall.PostAsync(environment, target.Address, path, new { }, cancellationToken);
        return answer.ExitCode != ExitCodes.Done
            ? (null, await target.FailedAsync(answer, cancellationToken))
            : (
                answer.Body?.Deserialize<AvailabilityAnswer>(Web) ?? throw new InvalidOperationException("the server sent no answer"),
                ExitCodes.Done
            );
    }

    private static AvailabilityChangeDocument Document(string bot, AvailabilityAnswer answer, bool dryRun) =>
        new(bot, answer.Kind, answer.Id, answer.Off, answer.Changed, answer.UsedBy, answer.Delivery, dryRun);
}
