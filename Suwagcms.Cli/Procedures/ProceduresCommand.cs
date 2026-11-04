using System.CommandLine;
using System.Text.Json;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Facts;
using Suwagcms.Cli.Output;
using Suwagcms.Contracts;

namespace Suwagcms.Cli.Procedures;

internal static class ProceduresCommand
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Command Create(GlobalOptions options, CliEnvironment environment) =>
        new("procedures", "List, show, add, edit and delete the procedures of a bot.")
        {
            CreateList(options, environment),
            CreateShow(options, environment),
            ProcedureSaveCommands.CreateAdd(options, environment),
            ProcedureSaveCommands.CreateEdit(options, environment),
            CreateDelete(options, environment),
        };

    private static void RefuseOpenWithJson(Command command, GlobalOptions options) =>
        command.Validators.Add(result =>
        {
            if (result.GetValue(options.Json) && result.GetValue(options.Open))
            {
                result.AddError("--json and --open cannot be used together: one prints the data, the other opens the page.");
            }
        });

    private static Command CreateList(GlobalOptions options, CliEnvironment environment)
    {
        var command = new Command(
            "list",
            "List the procedures of the bot, and what stops each from becoming a version (or --open the Procedures screen in the web app)."
        );
        RefuseOpenWithJson(command, options);
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await ProcedureTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                if (parsed.GetValue(options.Open))
                {
                    return await Open(target, parsed);
                }
                var answer = await ServerCall.GetAsync(environment, target.Address, target.ProceduresPath, cancellationToken);
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var list = answer.Body?.Deserialize<ProcedureList>(Web) ?? new ProcedureList([], []);
                return await target.Run.Report.DoneAsync(new ProcedureListDocument(target.Bot, list), ProcedureText.Table(list));
            }
        );
        return command;
    }

    private static Command CreateShow(GlobalOptions options, CliEnvironment environment)
    {
        var id = new Argument<string>("id") { Description = "The procedure's id, like orderValuation." };
        var command = new Command(
            "show",
            "Show one procedure as YAML (or JSON with --json), both accepted back by `procedures edit --file`."
        )
        {
            id,
        };
        RefuseOpenWithJson(command, options);
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await ProcedureTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                if (parsed.GetValue(options.Open))
                {
                    return await Open(target, parsed);
                }
                var answer = await ServerCall.GetAsync(
                    environment,
                    target.Address,
                    target.ProcedurePath(parsed.GetRequiredValue(id)),
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var body = answer.Body ?? throw new InvalidOperationException("the server sent no procedure");
                var procedure =
                    body.Deserialize<ProcedureDetail>(Web) ?? throw new InvalidOperationException("the server sent no procedure");
                return await target.Run.Report.DoneAsync(procedure, Yaml(body, procedure));
            }
        );
        return command;
    }

    // Why: the mistakes ride along as YAML comments, so the text is still a file that `edit --file` accepts back.
    private static string Yaml(JsonElement body, ProcedureDetail procedure)
    {
        var yaml = ProcedureYaml.From(JsonWithout(body, "notReady"));
        return procedure.NotReady.Count == 0
            ? yaml
            : $"{yaml}{Environment.NewLine}{string.Join(Environment.NewLine, ProcedureText.Mistakes("Not ready for a version", procedure.NotReady).Split(Environment.NewLine).Select(line => $"# {line}"))}";
    }

    private static JsonElement JsonWithout(JsonElement body, string property)
    {
        var node =
            System.Text.Json.Nodes.JsonNode.Parse(body.GetRawText()) as System.Text.Json.Nodes.JsonObject
            ?? throw new InvalidOperationException("the server sent no procedure");
        node.Remove(property);
        return JsonSerializer.SerializeToElement(node);
    }

    private static Command CreateDelete(GlobalOptions options, CliEnvironment environment)
    {
        var id = new Argument<string>("id") { Description = "The procedure's id. `conversation` cannot be deleted." };
        var ifRevision = ProcedureSaveCommands.IfRevisionOption();
        var dryRun = ProcedureSaveCommands.DryRunOption();
        var command = new Command("delete", "Delete a procedure. `conversation`, the base of every conversation, is always refused.")
        {
            id,
            ifRevision,
            dryRun,
        };
        command.Validators.Add(result =>
        {
            if (result.GetValue(options.Open))
            {
                result.AddError("`suwagcms procedures delete` has no web page to open: drop --open.");
            }
        });
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await ProcedureTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                var name = parsed.GetRequiredValue(id);
                var dry = parsed.GetValue(dryRun);
                var answer = await ServerCall.DeleteAsync(
                    environment,
                    target.Address,
                    target.ProcedurePath(name) + FactSaveCommands.Query(dry, parsed.GetValue(ifRevision)),
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                return await target.Run.Report.DoneAsync(
                    new ProcedureDeletedDocument(target.Bot, name, dry),
                    dry ? $"Would delete '{name}'. Nothing was changed." : $"Deleted '{name}'."
                );
            }
        );
        return command;
    }

    private static Task<int> Open(ProcedureTarget target, ParseResult parsed) =>
        WebPage.ShowAsync(
            target.Page,
            parsed.InvocationConfiguration.Output,
            parsed.InvocationConfiguration.Error,
            target.Machine.OpenBrowser
        );
}
