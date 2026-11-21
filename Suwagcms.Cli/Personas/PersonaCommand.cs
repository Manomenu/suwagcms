using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Cli.Output;
using Suwagcms.Cli.Procedures;
using Suwagcms.Contracts.Personas;

namespace Suwagcms.Cli.Personas;

internal static class PersonaCommand
{
    private const string Path = "persona";
    private const string OnePersona = "one persona: write a mapping with personality and scope";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Command Create(GlobalOptions options, CliEnvironment environment) =>
        new("persona", "Show and edit the persona of a bot: how it talks and what it talks about. It reaches the bot only with a version.")
        {
            CreateShow(options, environment),
            CreateEdit(options, environment),
        };

    private static Command CreateShow(GlobalOptions options, CliEnvironment environment)
    {
        var command = new Command(
            "show",
            "Print the persona as YAML, with its revision; `persona edit --file` takes this back with every text unchanged (or --open the Persona page in the web app)."
        );
        EditOptions.RefuseOpenWithJson(command, options);
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                if (parsed.GetValue(options.Open))
                {
                    await WebPage.ShowAsync(
                        target.PageOf(Path),
                        parsed.InvocationConfiguration.Output,
                        parsed.InvocationConfiguration.Error,
                        target.Machine.OpenBrowser
                    );
                    return ExitCodes.Done;
                }
                var answer = await ServerCall.GetAsync(environment, target.Address, target.CollectionPath(Path), cancellationToken);
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var persona = ViewOf(answer);
                return await target.Run.Report.DoneAsync(persona, PersonaText.Shown(persona, target.Bot));
            }
        );
        return command;
    }

    private static Command CreateEdit(GlobalOptions options, CliEnvironment environment)
    {
        var file = new Option<string>("--file")
        {
            Description = "A YAML or JSON file with the whole persona: personality, and scope with inScope, outOfScope and whenOutOfScope.",
            Required = true,
        };
        var ifRevision = new Option<int?>("--if-revision")
        {
            Description =
                "Apply only if the persona is still at this revision (see `persona show`); refused with the current revision otherwise. Default: the revision field of the file, else the latest.",
        };
        var dryRun = EditOptions.DryRun();
        var command = new Command(
            "edit",
            "Replace the whole persona with a YAML or JSON file: what the file leaves out becomes empty. A YAML block such as `personality: |` keeps its line breaks."
        )
        {
            file,
            ifRevision,
            dryRun,
        };
        EditOptions.OpenWhatItSaves(command, options, dryRun);
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                var (document, problem) = await ProcedureFile.ReadAsync(parsed.GetRequiredValue(file), cancellationToken, OnePersona);
                if (document is null)
                {
                    return await target.Run.Report.FailedAsync(ExitCodes.Misused, new FailureDocument(problem), problem);
                }
                var read = await ServerCall.GetAsync(environment, target.Address, target.CollectionPath(Path), cancellationToken);
                if (read.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(read, cancellationToken);
                }
                var dry = parsed.GetValue(dryRun);
                var expected = parsed.GetValue(ifRevision) ?? Revision(document) ?? ViewOf(read).Revision;
                document.Remove("revision");
                var answer = await ServerCall.PutAsync(
                    environment,
                    target.Address,
                    target.CollectionPath(Path) + EditOptions.Query(dry, expected),
                    document,
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(answer, cancellationToken);
                }
                var saved = ViewOf(answer);
                var reported = await target.Run.Report.DoneAsync(
                    new PersonaSavedDocument(target.Bot, saved, dry),
                    PersonaText.Saved(saved, target.Bot, dry)
                );
                return await target.OpenSavedAsync(reported, parsed, options, "Persona", Path);
            }
        );
        return command;
    }

    private static PersonaView ViewOf(ServerAnswer answer) =>
        answer.Body?.Deserialize<PersonaView>(Web) ?? throw new InvalidOperationException("the server sent no persona");

    private static int? Revision(JsonObject document) =>
        document["revision"] is JsonValue value && value.TryGetValue<int>(out var revision) ? revision : null;
}
