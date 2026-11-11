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
        var replace = new Option<bool>("--replace")
        {
            Description =
                "An `add` of a resource the CMS already holds replaces the whole of it, as `edit --file` would; one that would store what is stored leaves it as it is, revision and history too. Nothing the file does not name is deleted.",
        };
        var compare = new Option<bool>("--compare")
        {
            Description =
                "Say what applying the file with --replace would change, and change nothing: each change added, replaced with the fields and steps that differ, or unchanged, and what the CMS holds of the same kinds that the file does not name. Exit code 0 when nothing would change, 4 when something would, 1 when the file would be refused.",
        };
        var command = new Command(
            "apply",
            "Apply a file of changes all or nothing: order inside it does not matter, and every mistake comes back at once."
        )
        {
            file,
            dryRun,
            replace,
            compare,
        };
        EditOptions.RefuseOpen(command, options, "apply");
        command.Validators.Add(result =>
        {
            if (result.GetValue(compare) && result.GetValue(dryRun))
            {
                result.AddError("--compare changes nothing already: drop --dry-run.");
            }
        });
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
                var comparing = parsed.GetValue(compare);
                var dry = comparing || parsed.GetValue(dryRun);
                var replacing = comparing || parsed.GetValue(replace);
                var query = EditOptions.Query(dry, null);
                if (replacing)
                {
                    query += (query.Length == 0 ? "?" : "&") + "replace=true";
                }
                var answer = await ServerCall.PostAsync(
                    environment,
                    target.Address,
                    target.CollectionPath("changes") + query,
                    document,
                    cancellationToken
                );
                if (answer.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(WithHeading(answer, dry), cancellationToken);
                }
                var applied =
                    answer.Body?.Deserialize<ChangesApplied>(Web) ?? throw new InvalidOperationException("the server sent no result");
                if (!comparing)
                {
                    return await target.Run.Report.DoneAsync(applied, Summary(applied, replacing));
                }
                await target.Run.Report.DoneAsync(applied, CompareText.Of(applied));
                return CompareText.Differs(applied) ? ExitCodes.Differs : ExitCodes.Done;
            }
        );
        return command;
    }

    private static string Summary(ChangesApplied applied, bool replacing)
    {
        if (applied.Changes.Count == 0)
        {
            return CompareText.Nothing;
        }
        var text = new StringBuilder(
            $"{(applied.DryRun ? "Would apply" : "Applied")}: {(replacing ? CompareText.Counts(applied) : PlainCounts(applied))}."
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

    private static string PlainCounts(ChangesApplied applied) =>
        string.Create(CultureInfo.InvariantCulture, $"{applied.Added} added, {applied.Edited} edited, {applied.Deleted} deleted");

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
