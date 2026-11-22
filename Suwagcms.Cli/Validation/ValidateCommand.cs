using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Cli.Output;
using Suwagcms.Contracts.Validation;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Cli.Validation;

internal static class ValidateCommand
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static Command Create(GlobalOptions options, CliEnvironment environment)
    {
        var file = new Option<string>("--file") { Description = "A YAML or JSON file with a whole definition.", Required = true };
        var declaration = new Option<string?>("--declaration")
        {
            Description = "A YAML or JSON file with what a bot declares: also report what that bot cannot run.",
        };
        var stage = new Option<string>("--stage") { Description = "The stage to check at.", DefaultValueFactory = _ => "version" };
        stage.AcceptOnlyFromAmong("save", "version");
        var command = new Command(
            "validate",
            "Check a definition with the validator of the CMS, for a bot outside .NET: it stores nothing and contacts no bot. Exit code 0 for no mistakes, 1 otherwise."
        )
        {
            file,
            declaration,
            stage,
        };
        EditOptions.RefuseOpen(command, options, "validate");
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var run = BotCommandRun.Start(parsed, options, environment);
                if (run.Address is not { } address)
                {
                    return await run.BadAddressAsync();
                }
                var (definition, problem, unreadable) = await DefinitionFile.ReadAsync(parsed.GetRequiredValue(file), cancellationToken);
                if (definition is not { } document)
                {
                    return await ProblemAsync(run, problem, unreadable);
                }
                JsonElement? declared = null;
                if (parsed.GetValue(declaration) is { } path)
                {
                    (declared, problem, unreadable) = await DefinitionFile.ReadAsync(path, cancellationToken);
                    if (declared is null)
                    {
                        return await ProblemAsync(run, problem, unreadable);
                    }
                }
                var chosen = parsed.GetRequiredValue(stage) == "save" ? ValidationStage.Save : ValidationStage.Version;
                var answer = await ServerCall.PostAsync(
                    environment,
                    address,
                    "api/validate",
                    new ValidateDefinition(document, chosen, declared),
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await run.Report.ReportAsync(answer, address);
                }
                var result = answer.Body?.Deserialize<ValidationAnswer>(Web)
                    ?? throw new InvalidOperationException("the server sent no answer");
                return result.Mistakes.Count == 0
                    ? await run.Report.DoneAsync(result, $"No mistakes at the {chosen} stage.")
                    : await run.Report.FailedWithAsync(ExitCodes.Refused, result, Text(result));
            }
        );
        return command;
    }

    private static Task<int> ProblemAsync(BotCommandRun run, string problem, bool unreadable) =>
        run.Report.FailedAsync(unreadable ? ExitCodes.Misused : ExitCodes.Refused, new FailureDocument(problem), problem);

    private static string Text(ValidationAnswer answer) =>
        string.Join(
            Environment.NewLine,
            answer
                .Mistakes.Select(mistake => $"  {mistake.Code.Text()} {mistake.Message} {mistake.Fix}")
                .Prepend(string.Create(CultureInfo.InvariantCulture, $"Mistakes, {answer.Mistakes.Count}:"))
        );
}
