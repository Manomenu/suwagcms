using System.CommandLine;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Contracts.Procedures;

namespace Suwagcms.Cli.Procedures;

internal static class ProceduresCommand
{
    public const string Collection = "procedures";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Command Create(GlobalOptions options, CliEnvironment environment) =>
        new(
            "procedures",
            "List, show, add, edit and delete the procedures of a bot, and read or set where the web creator draws their steps."
        )
        {
            CreateList(options, environment),
            CreateShow(options, environment),
            ProcedureSaveCommands.CreateAdd(options, environment),
            ProcedureSaveCommands.CreateEdit(options, environment),
            CreateDelete(options, environment),
            ProcedureLayoutCommand.Create(options, environment),
        };

    private static Command CreateList(GlobalOptions options, CliEnvironment environment)
    {
        var command = new Command(
            "list",
            "List the procedures of the bot, and what stops each from becoming a version (or --open the Procedures screen in the web app)."
        );
        EditOptions.RefuseOpenWithJson(command, options);
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                if (parsed.GetValue(options.Open))
                {
                    return await target.OpenAsync(target.PageOf(ProceduresCommand.Collection), parsed);
                }
                var answer = await ServerCall.GetAsync(
                    environment,
                    target.Address,
                    target.CollectionPath(ProceduresCommand.Collection),
                    cancellationToken
                );
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
            "Show one procedure as YAML (or JSON with --json), both accepted back by `procedures edit --file`, or --open it in the web creator."
        )
        {
            id,
        };
        EditOptions.RefuseOpenWithJson(command, options);
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                if (parsed.GetValue(options.Open))
                {
                    return await target.OpenAsync(target.PageAt("Procedure", parsed.GetRequiredValue(id)), parsed);
                }
                var answer = await ServerCall.GetAsync(
                    environment,
                    target.Address,
                    target.ItemPath(ProceduresCommand.Collection, parsed.GetRequiredValue(id)),
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

    private static Command CreateDelete(GlobalOptions options, CliEnvironment environment) =>
        ResourceCommand.Delete(
            options,
            environment,
            "procedures delete",
            Collection,
            new Argument<string>("id") { Description = "The procedure's id. `conversation` cannot be deleted." },
            "Delete a procedure. `conversation`, the base of every conversation, is always refused.",
            (bot, name, dry) => new ProcedureDeletedDocument(bot, name, dry)
        );
}
