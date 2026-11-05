using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Cli.Output;
using Suwagcms.Cli.Procedures;
using Suwagcms.Contracts;

namespace Suwagcms.Cli.Actions;

internal static class ActionsCommand
{
    public const string Collection = "actions";

    private const string OneAction = "one action: write a mapping with id, description and implementation";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // Why: what `show` adds to the format, which the file of an edit may carry back and the server does not take.
    private static readonly string[] ShownOnly = ["id", "revision", "usedBy", "alsoDeclared", "noLongerDeclared"];

    public static Command Create(GlobalOptions options, CliEnvironment environment) =>
        new("actions", "List, show, add, edit and delete the actions of a bot. A secret is written as {secret:NAME}, never as its value.")
        {
            CreateList(options, environment),
            CreateShow(options, environment),
            CreateAdd(options, environment),
            CreateEdit(options, environment),
            CreateDelete(options, environment),
            ActionTestCommand.Create(options, environment),
        };

    private static Command CreateList(GlobalOptions options, CliEnvironment environment)
    {
        var command = new Command("list", "List the actions of the bot, each with its implementation (or --open the Actions screen).");
        EditOptions.RefuseOpenWithJson(command, options);
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                if (parsed.GetValue(options.Open))
                {
                    return await Open(target, parsed, target.PageOf(Collection));
                }
                var answer = await ServerCall.GetAsync(environment, target.Address, target.CollectionPath(Collection), cancellationToken);
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var actions = answer.Body?.Deserialize<List<ActionView>>(Web) ?? [];
                return await target.Run.Report.DoneAsync(new ActionListDocument(target.Bot, actions), ActionText.Table(actions));
            }
        );
        return command;
    }

    private static Command CreateShow(GlobalOptions options, CliEnvironment environment)
    {
        var id = new Argument<string>("id") { Description = "The action's id, like checkServiceability." };
        var command = new Command(
            "show",
            "Show one action as YAML (or JSON with --json), both accepted back by `actions edit --file`, or --open it in the web."
        )
        {
            id,
        };
        EditOptions.RefuseOpenWithJson(command, options);
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                var name = parsed.GetRequiredValue(id);
                if (parsed.GetValue(options.Open))
                {
                    return await Open(target, parsed, new Uri(target.PageOf(Collection), $"?open={Uri.EscapeDataString(name)}"));
                }
                var answer = await ServerCall.GetAsync(environment, target.Address, target.ItemPath(Collection, name), cancellationToken);
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var body = answer.Body ?? throw new InvalidOperationException("the server sent no action");
                return await target.Run.Report.DoneAsync(Action(body), ProcedureYaml.From(body));
            }
        );
        return command;
    }

    private static Command CreateAdd(GlobalOptions options, CliEnvironment environment)
    {
        var file = FileOption();
        var dryRun = EditOptions.DryRun();
        var command = new Command("add", "Add an action to the bot from a YAML or JSON file that holds the whole of it.") { file, dryRun };
        EditOptions.RefuseOpen(command, options, "actions add");
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                var path = parsed.GetRequiredValue(file);
                var (document, problem) = await ProcedureFile.ReadAsync(path, cancellationToken, OneAction);
                if (document is null)
                {
                    return await MisusedAsync(target, problem);
                }
                if (Id(document) is not { Length: > 0 } id)
                {
                    return await MisusedAsync(
                        target,
                        $"'{path}' has no id. Add 'id: <name>' to the file: the name is camelCase, like checkServiceability."
                    );
                }
                var dry = parsed.GetValue(dryRun);
                var answer = await ServerCall.PutAsync(
                    environment,
                    target.Address,
                    target.ItemPath(Collection, id) + EditOptions.Query(dry, 0),
                    Body(document),
                    cancellationToken
                );
                return await ReportSavedAsync(target, answer, dry, "add", "Added", cancellationToken);
            }
        );
        return command;
    }

    private static Command CreateEdit(GlobalOptions options, CliEnvironment environment)
    {
        var id = new Argument<string>("id")
        {
            Description = "The action's id. It does not change here: `suwagcms rename action` changes it.",
        };
        var file = FileOption();
        var ifRevision = EditOptions.IfRevision();
        var dryRun = EditOptions.DryRun();
        var command = new Command(
            "edit",
            "Replace an action with the whole of a YAML or JSON file (the output of `actions show` is accepted back)."
        )
        {
            id,
            file,
            ifRevision,
            dryRun,
        };
        EditOptions.RefuseOpen(command, options, "actions edit");
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                var name = parsed.GetRequiredValue(id);
                var (document, problem) = await ProcedureFile.ReadAsync(parsed.GetRequiredValue(file), cancellationToken, OneAction);
                if (document is null)
                {
                    return await MisusedAsync(target, problem);
                }
                if (Id(document) is { Length: > 0 } inFile && inFile != name)
                {
                    return await MisusedAsync(
                        target,
                        $"The command edits '{name}' but the file holds '{inFile}'. Use the same id in both: change the id in the file, or the one on the command line."
                    );
                }
                var read = await ServerCall.GetAsync(environment, target.Address, target.ItemPath(Collection, name), cancellationToken);
                if (read.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(read, cancellationToken);
                }
                // Why: a file read from `show` carries its revision, so an edit of it is refused when someone saved in between; with neither, the edit guards the revision it just read.
                var expected = parsed.GetValue(ifRevision) ?? Revision(document) ?? Action(read.Body ?? default).Revision;
                var dry = parsed.GetValue(dryRun);
                var answer = await ServerCall.PutAsync(
                    environment,
                    target.Address,
                    target.ItemPath(Collection, name) + EditOptions.Query(dry, expected),
                    Body(document),
                    cancellationToken
                );
                return await ReportSavedAsync(target, answer, dry, "edit", "Saved", cancellationToken);
            }
        );
        return command;
    }

    private static Command CreateDelete(GlobalOptions options, CliEnvironment environment)
    {
        var id = new Argument<string>("id") { Description = "The action's id." };
        var ifRevision = EditOptions.IfRevision();
        var dryRun = EditOptions.DryRun();
        var command = new Command("delete", "Delete an action that no procedure uses.") { id, ifRevision, dryRun };
        EditOptions.RefuseOpen(command, options, "actions delete");
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await BotTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                var name = parsed.GetRequiredValue(id);
                var dry = parsed.GetValue(dryRun);
                var answer = await ServerCall.DeleteAsync(
                    environment,
                    target.Address,
                    target.ItemPath(Collection, name) + EditOptions.Query(dry, parsed.GetValue(ifRevision)),
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                return await target.Run.Report.DoneAsync(
                    new ActionDeletedDocument(target.Bot, name, dry),
                    dry ? $"Would delete '{name}'. Nothing was changed." : $"Deleted '{name}'."
                );
            }
        );
        return command;
    }

    private static Option<string> FileOption() =>
        new("--file")
        {
            Description = "A YAML or JSON file with the whole action: id, description, parameters, results, implementation.",
            Required = true,
        };

    private static ActionView Action(JsonElement body) =>
        body.Deserialize<ActionView>(Web) ?? throw new InvalidOperationException("the server sent no action");

    private static string? Id(JsonObject document) =>
        document["id"] is JsonValue value && value.TryGetValue<string>(out var id) ? id : null;

    private static int? Revision(JsonObject document) =>
        document["revision"] is JsonValue value && value.TryGetValue<int>(out var revision) ? revision : null;

    private static JsonObject Body(JsonObject document)
    {
        var body = (JsonObject)document.DeepClone();
        foreach (var name in ShownOnly)
        {
            body.Remove(name);
        }
        return body;
    }

    private static Task<int> Open(BotTarget target, ParseResult parsed, Uri page) =>
        WebPage.ShowAsync(page, parsed.InvocationConfiguration.Output, parsed.InvocationConfiguration.Error, target.Machine.OpenBrowser);

    private static Task<int> MisusedAsync(BotTarget target, string text) =>
        target.Run.Report.FailedAsync(ExitCodes.Misused, new FailureDocument(text), text);

    private static async Task<int> ReportSavedAsync(
        BotTarget target,
        ServerAnswer answer,
        bool dry,
        string word,
        string past,
        CancellationToken cancellationToken
    )
    {
        if (answer.ExitCode != ExitCodes.Done)
        {
            return await target.FailedAsync(answer, cancellationToken);
        }
        var action = Action(answer.Body ?? default);
        return await target.Run.Report.DoneAsync(
            new ActionSavedDocument(target.Bot, action, dry),
            dry
                ? $"Would {word} '{action.Id}'. Nothing was changed."
                : string.Create(CultureInfo.InvariantCulture, $"{past} {action.Id}, revision {action.Revision}.")
        );
    }
}
