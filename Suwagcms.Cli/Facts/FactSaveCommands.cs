using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Suwagcms.Cli.Connection;
using Suwagcms.Contracts;

namespace Suwagcms.Cli.Facts;

internal static class FactSaveCommands
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private sealed record Fields(
        Option<string?> Type,
        Option<string?> Description,
        Option<string?> WritableBy,
        Option<string[]?> Values,
        Option<int?> Min,
        Option<int?> Max,
        Option<int?> MaxLength
    );

    public static Option<int?> IfRevisionOption() =>
        new("--if-revision")
        {
            Description =
                "Apply only if the fact is still at this revision (see `facts show`); refused with the current revision otherwise.",
        };

    public static Option<bool> DryRunOption() =>
        new("--dry-run") { Description = "Answer as the real command would, with the same refusals, and change nothing." };

    public static string Query(bool dryRun, int? ifRevision)
    {
        var parts = new List<string>();
        if (ifRevision is { } revision)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"ifRevision={revision}"));
        }
        if (dryRun)
        {
            parts.Add("dryRun=true");
        }
        return parts.Count == 0 ? "" : "?" + string.Join('&', parts);
    }

    // Why: the options are left optional so that a missing one reaches the server, which reports every mistake at once.
    private static Fields FieldOptions() =>
        new(
            new Option<string?>("--type") { Description = "enum, int or text. Changing the type of a fact drops its old constraints." },
            new Option<string?>("--description") { Description = "What the fact means; the model reads it." },
            new Option<string?>("--writable-by") { Description = "assign, set or correct. Default for a new fact: set." },
            new Option<string[]?>("--values")
            {
                Description = "The values of an enum, at least two.",
                AllowMultipleArgumentsPerToken = true,
            },
            new Option<int?>("--min") { Description = "The lowest value of an int." },
            new Option<int?>("--max") { Description = "The highest value of an int." },
            new Option<int?>("--max-length") { Description = "The most characters of a text, above 0." }
        );

    public static Command CreateAdd(GlobalOptions options, CliEnvironment environment)
    {
        var name = new Argument<string>("name")
        {
            Description = "The fact's name: camelCase words, which may be joined by dots (customer.city).",
        };
        var fields = FieldOptions();
        var dryRun = DryRunOption();
        var command = new Command("add", "Add a global fact to the bot.") { name };
        AddFields(command, fields);
        command.Options.Add(dryRun);
        RefuseOpen(command, options, "add");
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await FactTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                var factName = parsed.GetRequiredValue(name);
                var body = new SaveFact(
                    parsed.GetValue(fields.Type),
                    parsed.GetValue(fields.Description),
                    parsed.GetValue(fields.WritableBy),
                    Given(parsed, fields.Values),
                    parsed.GetValue(fields.Min),
                    parsed.GetValue(fields.Max),
                    parsed.GetValue(fields.MaxLength)
                );
                var dry = parsed.GetValue(dryRun);
                var answer = await ServerCall.PutAsync(
                    environment,
                    target.Address,
                    target.FactPath(factName) + Query(dry, 0),
                    body,
                    cancellationToken
                );
                return await ReportSavedAsync(target, answer, dry, "add", cancellationToken);
            }
        );
        return command;
    }

    public static Command CreateEdit(GlobalOptions options, CliEnvironment environment)
    {
        var name = new Argument<string>("name") { Description = "The fact's name, like customer.city. It does not change." };
        var fields = FieldOptions();
        var ifRevision = IfRevisionOption();
        var dryRun = DryRunOption();
        var command = new Command("edit", "Change a global fact: only the options you give change, the other fields stay.") { name };
        AddFields(command, fields);
        command.Options.Add(ifRevision);
        command.Options.Add(dryRun);
        RefuseOpen(command, options, "edit");
        command.Validators.Add(result =>
        {
            if (
                result.GetValue(fields.Type) is null
                && result.GetValue(fields.Description) is null
                && result.GetValue(fields.WritableBy) is null
                && result.GetResult(fields.Values) is null
                && result.GetValue(fields.Min) is null
                && result.GetValue(fields.Max) is null
                && result.GetValue(fields.MaxLength) is null
            )
            {
                result.AddError(
                    "Nothing to change: give at least one of --type, --description, --writable-by, --values, --min, --max, --max-length."
                );
            }
        });
        command.SetAction(
            async (parsed, cancellationToken) =>
            {
                var (target, exit) = await FactTarget.ResolveAsync(parsed, options, environment, cancellationToken);
                if (target is null)
                {
                    return exit;
                }
                var factName = parsed.GetRequiredValue(name);
                var read = await ServerCall.GetAsync(environment, target.Address, target.FactPath(factName), cancellationToken);
                if (read.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(read, cancellationToken);
                }
                var current = read.Body?.Deserialize<Fact>(Web) ?? throw new InvalidOperationException("the server sent no fact");
                var type = parsed.GetValue(fields.Type);
                // Why: constraints belong to a type, so changing the type starts from none and the options give the new ones.
                var kept =
                    type is null || type == current.Type
                        ? current
                        : current with
                        {
                            Values = null,
                            Min = null,
                            Max = null,
                            MaxLength = null,
                        };
                var body = new SaveFact(
                    type ?? current.Type,
                    parsed.GetValue(fields.Description) ?? current.Description,
                    parsed.GetValue(fields.WritableBy) ?? current.WritableBy,
                    Given(parsed, fields.Values) ?? kept.Values,
                    parsed.GetValue(fields.Min) ?? kept.Min,
                    parsed.GetValue(fields.Max) ?? kept.Max,
                    parsed.GetValue(fields.MaxLength) ?? kept.MaxLength
                );
                var dry = parsed.GetValue(dryRun);
                // Why: without --if-revision the edit still guards the fields it just read, so a change made in between is reported, never overwritten.
                var answer = await ServerCall.PutAsync(
                    environment,
                    target.Address,
                    target.FactPath(factName) + Query(dry, parsed.GetValue(ifRevision) ?? current.Revision),
                    body,
                    cancellationToken
                );
                return await ReportSavedAsync(target, answer, dry, "edit", cancellationToken);
            }
        );
        return command;
    }

    // Why: an array option reads as an empty list when absent, and an edit must tell "not given" from "given".
    private static string[]? Given(ParseResult parsed, Option<string[]?> values) =>
        parsed.GetResult(values) is null ? null : parsed.GetValue(values);

    private static void AddFields(Command command, Fields fields)
    {
        command.Options.Add(fields.Type);
        command.Options.Add(fields.Description);
        command.Options.Add(fields.WritableBy);
        command.Options.Add(fields.Values);
        command.Options.Add(fields.Min);
        command.Options.Add(fields.Max);
        command.Options.Add(fields.MaxLength);
    }

    private static void RefuseOpen(Command command, GlobalOptions options, string word) =>
        command.Validators.Add(result =>
        {
            if (result.GetValue(options.Open))
            {
                result.AddError($"`suwagcms facts {word}` has no web page to open: drop --open.");
            }
        });

    private static async Task<int> ReportSavedAsync(
        FactTarget target,
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
        var fact = answer.Body?.Deserialize<Fact>(Web) ?? throw new InvalidOperationException("the server sent no fact");
        var past = word == "add" ? "Added" : "Edited";
        var text = dry
            ? $"Would {word} '{fact.Name}' ({FactText.Constraint(fact)}). Nothing was changed."
            : string.Create(CultureInfo.InvariantCulture, $"{past} '{fact.Name}' ({FactText.Constraint(fact)}), revision {fact.Revision}.");
        return await target.Run.Report.DoneAsync(new FactSavedDocument(target.Bot, fact, dry), text);
    }
}
