using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Contracts.Procedures;

namespace Suwagcms.Cli.Procedures;

internal static class ProcedureSaveCommands
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static Option<int?> IfRevisionOption() =>
        new("--if-revision")
        {
            Description =
                "Apply only if the procedure is still at this revision (see `procedures show`); refused with the current revision otherwise. Default: the revision field of the file, else the latest.",
        };

    public static Command CreateAdd(GlobalOptions options, CliEnvironment environment)
    {
        var file = FileOption();
        var dryRun = EditOptions.DryRun();
        var command = new Command(
            "add",
            "Add a procedure to the bot from a YAML or JSON file that holds the whole of it, and with --open show it in the web creator."
        )
        {
            file,
            dryRun,
        };
        EditOptions.RefuseOpenWithJson(command, options);
        command.Validators.Add(result =>
        {
            if (result.GetValue(options.Open) && result.GetValue(dryRun))
            {
                result.AddError("--dry-run and --open cannot be used together: a dry run saves nothing, so there is no procedure to open.");
            }
        });
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                var (document, problem) = await ProcedureFile.ReadAsync(parsed.GetRequiredValue(file), cancellationToken);
                if (document is null)
                {
                    return await target.MisusedAsync(problem);
                }
                if (Id(document) is not { Length: > 0 } id)
                {
                    return await target.MisusedAsync(
                        $"'{parsed.GetRequiredValue(file)}' has no id. Add 'id: <name>' to the file: the name is camelCase, like orderValuation."
                    );
                }
                var dry = parsed.GetValue(dryRun);
                var answer = await ServerCall.PutAsync(
                    environment,
                    target.Address,
                    target.ItemPath(ProceduresCommand.Collection, id) + EditOptions.Query(dry, 0),
                    Body(document),
                    cancellationToken
                );
                var reported = await ReportSavedAsync(target, answer, dry, "add", cancellationToken);
                return reported == ExitCodes.Done && parsed.GetValue(options.Open)
                    ? await target.OpenAsync(target.PageAt("Procedure", id), parsed)
                    : reported;
            }
        );
        return command;
    }

    public static Command CreateEdit(GlobalOptions options, CliEnvironment environment)
    {
        var id = new Argument<string>("id") { Description = "The procedure's id, like orderValuation. It does not change." };
        var file = FileOption();
        var ifRevision = IfRevisionOption();
        var dryRun = EditOptions.DryRun();
        var command = new Command(
            "edit",
            "Replace a procedure with the whole of a YAML or JSON file (the output of `procedures show` is accepted back)."
        )
        {
            id,
            file,
            ifRevision,
            dryRun,
        };
        EditOptions.RefuseOpen(command, options, "procedures edit");
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                var name = parsed.GetRequiredValue(id);
                var (document, problem) = await ProcedureFile.ReadAsync(parsed.GetRequiredValue(file), cancellationToken);
                if (document is null)
                {
                    return await target.MisusedAsync(problem);
                }
                if (Id(document) is { Length: > 0 } inFile && inFile != name)
                {
                    return await target.MisusedAsync(
                        $"The command edits '{name}' but the file holds '{inFile}'. Use the same id in both: change the id in the file, or the one on the command line."
                    );
                }
                var read = await ServerCall.GetAsync(
                    environment,
                    target.Address,
                    target.ItemPath(ProceduresCommand.Collection, name),
                    cancellationToken
                );
                if (read.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(read, cancellationToken);
                }
                var current =
                    read.Body?.Deserialize<ProcedureDetail>(Web) ?? throw new InvalidOperationException("the server sent no procedure");
                var dry = parsed.GetValue(dryRun);
                // Why: a file read from `show` carries its revision, so an edit of it is refused when someone saved in between; with neither, the edit guards the revision it just read.
                var expected = parsed.GetValue(ifRevision) ?? Revision(document) ?? current.Revision;
                var answer = await ServerCall.PutAsync(
                    environment,
                    target.Address,
                    target.ItemPath(ProceduresCommand.Collection, name) + EditOptions.Query(dry, expected),
                    Body(document),
                    cancellationToken
                );
                return await ReportSavedAsync(target, answer, dry, "edit", cancellationToken);
            }
        );
        return command;
    }

    private static Option<string> FileOption() =>
        new("--file")
        {
            Description =
                "A YAML or JSON file with the whole procedure: id, description, firstStep, steps, whenStalled, whenInterrupted, localFacts.",
            Required = true,
        };

    private static string? Id(JsonObject document) =>
        document["id"] is JsonValue value && value.TryGetValue<string>(out var id) ? id : null;

    private static int? Revision(JsonObject document) =>
        document["revision"] is JsonValue value && value.TryGetValue<int>(out var revision) ? revision : null;

    // Why: the address names the procedure and the server stores the revision, so the id and what `show` adds are not part of what is sent.
    private static JsonObject Body(JsonObject document)
    {
        var body = (JsonObject)document.DeepClone();
        body.Remove("id");
        body.Remove("revision");
        body.Remove("notReady");
        return body;
    }

    private static async Task<int> ReportSavedAsync(
        BotTarget target,
        ServerAnswer answer,
        bool dry,
        string word,
        CancellationToken cancellationToken
    )
    {
        if (answer.ExitCode != ExitCodes.Done)
        {
            return await target.FailedAsync(answer, cancellationToken);
        }
        var procedure =
            answer.Body?.Deserialize<ProcedureDetail>(Web) ?? throw new InvalidOperationException("the server sent no procedure");
        return await target.Run.Report.DoneAsync(
            new ProcedureSavedDocument(target.Bot, procedure, dry),
            ProcedureText.Saved(procedure, dry, word == "add" ? "Added" : "Saved", word)
        );
    }
}
