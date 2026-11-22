using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Cli.History;
using Suwagcms.Contracts.Bots;
using Suwagcms.Contracts.History;
using Suwagcms.Contracts.Versions;

namespace Suwagcms.Cli.Versions;

internal static class VersionsCommand
{
    public const string Collection = "versions";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Command Create(GlobalOptions options, CliEnvironment environment) =>
        new("versions", "List the versions of a bot, create one from its working state, show one, or show what happened before it.")
        {
            CreateList(options, environment),
            CreateCreate(options, environment),
            CreateShow(options, environment),
            CreateWindow(options, environment),
            CreateStatus(options, environment),
        };

    private static Command CreateList(GlobalOptions options, CliEnvironment environment)
    {
        var command = new Command("list", "List the versions of the bot, newest first (or --open the Versions screen in the web app).");
        EditOptions.RefuseOpenWithJson(command, options);
        command.SetBotAction(options, environment, async (target, parsed, cancellationToken) =>
        {
            if (parsed.GetValue(options.Open))
            {
                return await target.OpenAsync(target.PageOf(Collection), parsed);
            }
            var answer = await ServerCall.GetAsync(environment, target.Address, target.CollectionPath(Collection), cancellationToken);
            if (answer.ExitCode != ExitCodes.Done)
            {
                return await target.FailedAsync(answer, cancellationToken);
            }
            var versions = answer.Body?.Deserialize<List<VersionSummary>>(Web) ?? [];
            return await target.Run.Report.DoneAsync(new VersionListDocument(target.Bot, versions), VersionText.Table(versions));
        });
        return command;
    }

    private static Command CreateCreate(GlobalOptions options, CliEnvironment environment)
    {
        var dryRun = EditOptions.DryRun();
        var command = new Command(
            "create",
            "Create a version from the bot's working state: refused with every mistake while the state is unfinished or has not changed since the latest version."
        )
        {
            dryRun,
        };
        EditOptions.RefuseOpen(command, options, "versions create");
        command.SetBotAction(options, environment, async (target, parsed, cancellationToken) =>
        {
            var answer = await ServerCall.PostAsync(
                environment,
                target.Address,
                target.CollectionPath(Collection) + EditOptions.Query(parsed.GetValue(dryRun), null),
                new { },
                cancellationToken
            );
            if (answer.ExitCode == ExitCodes.Refused)
            {
                var heading = string.Create(CultureInfo.InvariantCulture, $"Not created, {answer.Mistakes.Count} mistake(s):");
                return await target.FailedAsync(
                    answer with
                    {
                        Message = $"{heading}{Environment.NewLine}{answer.Message}",
                    },
                    cancellationToken
                );
            }
            if (answer.ExitCode != ExitCodes.Done)
            {
                return await target.FailedAsync(answer, cancellationToken);
            }
            var created = answer.Body?.Deserialize<VersionCreated>(Web)
                ?? throw new InvalidOperationException("the server sent no version");
            return await target.Run.Report.DoneAsync(created, VersionText.Created(created));
        });
        return command;
    }

    private static Command CreateStatus(GlobalOptions options, CliEnvironment environment)
    {
        var command = new Command(
            "status",
            "Ask the bot which version it has (never what the CMS last sent), with the latest version, the versions still in use and the recent pushes (or --open the Versions screen in the web app)."
        );
        EditOptions.RefuseOpenWithJson(command, options);
        command.SetBotAction(options, environment, async (target, parsed, cancellationToken) =>
        {
            if (parsed.GetValue(options.Open))
            {
                return await target.OpenAsync(target.PageOf(Collection), parsed);
            }
            var answer = await ServerCall.GetAsync(
                environment,
                target.Address,
                target.CollectionPath($"{Collection}/status"),
                cancellationToken
            );
            if (answer.ExitCode != ExitCodes.Done)
            {
                return await target.FailedAsync(answer, cancellationToken);
            }
            var status = answer.Body?.Deserialize<BotStatus>(Web) ?? throw new InvalidOperationException("the server sent no status");
            return await target.Run.Report.DoneAsync(status, VersionText.Status(status));
        });
        return command;
    }

    private static Command CreateWindow(GlobalOptions options, CliEnvironment environment)
    {
        var number = new Argument<int>("number") { Description = "The version's number, like 14." };
        var limit = HistoryCommand.LimitOption();
        var before = HistoryCommand.BeforeOption();
        var page = HistoryCommand.PageOption();
        var command = new Command(
            "window",
            "Show what happened from the previous version's creation through this version's, newest first (or --open the version's page)."
        )
        {
            number,
            limit,
            before,
            page,
        };
        EditOptions.RefuseOpenWithJson(command, options);
        HistoryCommand.RefuseWrongPaging(command, limit, before, page);
        command.SetBotAction(options, environment, async (target, parsed, cancellationToken) =>
        {
            var version = parsed.GetValue(number).ToString(CultureInfo.InvariantCulture);
            if (parsed.GetValue(options.Open))
            {
                var asked = parsed.GetValue(page) is { } wanted
                    ? string.Create(CultureInfo.InvariantCulture, $"?historyPage={wanted}")
                    : "";
                return await target.OpenAsync(new Uri(target.PageOf($"{Collection}/{version}"), asked), parsed);
            }
            var answer = await ServerCall.GetAsync(
                environment,
                target.Address,
                target.ItemPath(Collection, version)
                    + "/window"
                    + HistoryCommand.Query(parsed.GetValue(limit), parsed.GetValue(before), parsed.GetValue(page)),
                cancellationToken
            );
            if (answer.ExitCode != ExitCodes.Done)
            {
                return await target.FailedAsync(answer, cancellationToken);
            }
            var shown = answer.Body?.Deserialize<HistoryPage>(Web) ?? throw new InvalidOperationException("the server sent no window");
            return await target.Run.Report.DoneAsync(shown, HistoryText.Window(shown, parsed.GetValue(page)));
        });
        return command;
    }

    private static Command CreateShow(GlobalOptions options, CliEnvironment environment)
    {
        var number = new Argument<int>("number") { Description = "The version's number, like 14." };
        var command = new Command(
            "show",
            "Show one version with its document exactly as stored (or --open the version's page in the web app)."
        )
        {
            number,
        };
        EditOptions.RefuseOpenWithJson(command, options);
        command.SetBotAction(options, environment, async (target, parsed, cancellationToken) =>
        {
            var shownNumber = parsed.GetRequiredValue(number).ToString(CultureInfo.InvariantCulture);
            if (parsed.GetValue(options.Open))
            {
                return await target.OpenAsync(target.PageOf($"{Collection}/{shownNumber}"), parsed);
            }
            var answer = await ServerCall.GetAsync(
                environment,
                target.Address,
                target.ItemPath(Collection, shownNumber),
                cancellationToken
            );
            if (answer.ExitCode != ExitCodes.Done)
            {
                return await target.FailedAsync(answer, cancellationToken);
            }
            var detail = answer.Body?.Deserialize<VersionDetail>(Web) ?? throw new InvalidOperationException("the server sent no version");
            return await target.Run.Report.DoneAsync(detail, VersionText.Detail(detail));
        });
        return command;
    }
}
