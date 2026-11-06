using System.CommandLine;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Cli.Output;
using Suwagcms.Contracts.Changes;

namespace Suwagcms.Cli.Changes;

internal static class ApplyCommand
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public static Command Create(GlobalOptions options, CliEnvironment environment)
    {
        var file = new Argument<string>("file") { Description = "A YAML or JSON file with a `changes` list of add, edit and delete." };
        var dryRun = EditOptions.DryRun();
        var command = new Command(
            "apply",
            "Apply a file of changes all or nothing: order inside it does not matter, and every mistake comes back at once."
        )
        {
            file,
            dryRun,
        };
        EditOptions.RefuseOpen(command, options, "apply");
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                var (document, problem) = await ChangeFile.ReadAsync(parsed.GetRequiredValue(file), cancellationToken);
                if (document is null)
                {
                    return await target.Run.Report.FailedAsync(ExitCodes.Misused, new FailureDocument(problem), problem);
                }
                var dry = parsed.GetValue(dryRun);
                var answer = await ServerCall.PostAsync(
                    environment,
                    target.Address,
                    target.CollectionPath("changes") + EditOptions.Query(dry, null),
                    document,
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(WithHeading(answer, dry), cancellationToken);
                }
                var applied =
                    answer.Body?.Deserialize<ChangesApplied>(Web) ?? throw new InvalidOperationException("the server sent no result");
                return await target.Run.Report.DoneAsync(applied, Summary(applied));
            }
        );
        return command;
    }

    private static string Summary(ChangesApplied applied)
    {
        if (applied.Changes.Count == 0)
        {
            return "Nothing to apply: the file lists no changes.";
        }
        var text = new StringBuilder(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{(applied.DryRun ? "Would apply" : "Applied")}: {applied.Added} added, {applied.Edited} edited, {applied.Deleted} deleted."
            )
        );
        if (applied.NotReady.Count > 0)
        {
            text.Append(Environment.NewLine).Append("What still stops a version:");
            foreach (var mistake in applied.NotReady)
            {
                text.Append(Environment.NewLine).Append($"  {mistake.Code}  {mistake.Message}");
            }
        }
        return text.ToString();
    }

    // Why: an unknown bot keeps the shared answer, which lists the bots; only a refused file says how many mistakes and where each one is.
    private static ServerAnswer WithHeading(ServerAnswer answer, bool dry)
    {
        if (answer.Mistakes.Count == 0 || !answer.Mistakes.Any(mistake => mistake.TryGetProperty("change", out _)))
        {
            return answer;
        }
        var heading = string.Create(
            CultureInfo.InvariantCulture,
            $"{(dry ? "Would not apply" : "Not applied")}, {answer.Mistakes.Count} mistake(s):"
        );
        return answer with { Message = string.Join(Environment.NewLine, [heading, .. answer.Mistakes.Select(Line)]) };
    }

    private static string Line(JsonElement mistake)
    {
        var where = new List<string>();
        if (mistake.GetProperty("change") is { ValueKind: JsonValueKind.Number } position)
        {
            where.Add(string.Create(CultureInfo.InvariantCulture, $"change {position.GetInt32()}"));
        }
        var resource = mistake.GetProperty("resource");
        var kind = resource.GetProperty("kind").GetString() ?? "";
        if (kind != "Definition")
        {
            where.Add($"{ServerAnswer.Noun(kind)} '{resource.GetProperty("id").GetString()}'");
        }
        var head = where.Count == 0 ? "" : string.Join(", ", where) + "  ";
        return $"  {head}{ServerAnswer.Describe([mistake], null).Replace(Environment.NewLine, Environment.NewLine + "  ", StringComparison.Ordinal)}";
    }
}
