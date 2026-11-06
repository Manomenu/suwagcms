using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using Suwagcms.Cli.Bots;
using Suwagcms.Cli.Connection;
using Suwagcms.Cli.Editing;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;

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
        Option<int?> MaxLength,
        Option<string?> PatternRegex,
        Option<string?> PatternMeans,
        Option<string?> PatternExample,
        Option<string?> PatternTellCustomer,
        Option<bool> NoPattern
    );

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
            new Option<int?>("--max-length") { Description = "The most characters of a text, above 0." },
            new Option<string?>("--pattern-regex") { Description = "The regular expression a whole value of a text must match." },
            new Option<string?>("--pattern-means") { Description = "In words, what a value of the pattern looks like." },
            new Option<string?>("--pattern-example") { Description = "A value the regex accepts; an empty text removes it." },
            new Option<string?>("--pattern-tell-customer") { Description = "upfront or afterRefusal. Default for a new pattern: upfront." },
            new Option<bool>("--no-pattern") { Description = "Take the pattern off a text." }
        );

    public static Command CreateAdd(GlobalOptions options, CliEnvironment environment)
    {
        var name = new Argument<string>("name")
        {
            Description = "The fact's name: camelCase words, which may be joined by dots (customer.city).",
        };
        var fields = FieldOptions();
        var dryRun = EditOptions.DryRun();
        var command = new Command("add", "Add a global fact to the bot.") { name };
        AddFields(command, fields);
        command.Options.Add(dryRun);
        EditOptions.RefuseOpen(command, options, "facts add");
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                var factName = parsed.GetRequiredValue(name);
                var body = new SaveFact(
                    parsed.GetValue(fields.Type),
                    parsed.GetValue(fields.Description),
                    parsed.GetValue(fields.WritableBy),
                    Given(parsed, fields.Values),
                    parsed.GetValue(fields.Min),
                    parsed.GetValue(fields.Max),
                    parsed.GetValue(fields.MaxLength),
                    PatternGiven(parsed, fields, null)
                );
                var dry = parsed.GetValue(dryRun);
                var answer = await ServerCall.PutAsync(
                    environment,
                    target.Address,
                    target.ItemPath(FactsCommand.Collection, factName) + EditOptions.Query(dry, 0),
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
        var ifRevision = EditOptions.IfRevision();
        var dryRun = EditOptions.DryRun();
        var command = new Command("edit", "Change a global fact: only the options you give change, the other fields stay.") { name };
        AddFields(command, fields);
        command.Options.Add(ifRevision);
        command.Options.Add(dryRun);
        EditOptions.RefuseOpen(command, options, "facts edit");
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
                && !PatternOptions(fields).Any(option => result.GetResult(option) is not null)
                && !result.GetValue(fields.NoPattern)
            )
            {
                result.AddError(
                    "Nothing to change: give at least one of --type, --description, --writable-by, --values, --min, --max, --max-length, --pattern-regex, --pattern-means, --pattern-example, --pattern-tell-customer, --no-pattern."
                );
            }
        });
        command.SetBotAction(
            options,
            environment,
            async (target, parsed, cancellationToken) =>
            {
                var factName = parsed.GetRequiredValue(name);
                var read = await ServerCall.GetAsync(
                    environment,
                    target.Address,
                    target.ItemPath(FactsCommand.Collection, factName),
                    cancellationToken
                );
                if (read.ExitCode != ExitCodes.Done)
                {
                    return await target.FailedAsync(read, cancellationToken);
                }
                var current = read.Body?.Deserialize<Fact>(Web) ?? throw new InvalidOperationException("the server sent no fact");
                var type = parsed.GetValue(fields.Type);
                var keptPattern = type is null || type == current.Type ? current.Pattern : null;
                var body = EditOverlay.OverFact(
                    current,
                    new SaveFact(
                        type,
                        parsed.GetValue(fields.Description),
                        parsed.GetValue(fields.WritableBy),
                        Given(parsed, fields.Values),
                        parsed.GetValue(fields.Min),
                        parsed.GetValue(fields.Max),
                        parsed.GetValue(fields.MaxLength),
                        PatternGiven(parsed, fields, keptPattern)
                    ),
                    parsed.GetValue(fields.NoPattern)
                );
                var dry = parsed.GetValue(dryRun);
                // Why: without --if-revision the edit still guards the fields it just read, so a change made in between is reported, never overwritten.
                var answer = await ServerCall.PutAsync(
                    environment,
                    target.Address,
                    target.ItemPath(FactsCommand.Collection, factName)
                        + EditOptions.Query(dry, parsed.GetValue(ifRevision) ?? current.Revision),
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

    private static Option<string?>[] PatternOptions(Fields fields) =>
        [fields.PatternRegex, fields.PatternMeans, fields.PatternExample, fields.PatternTellCustomer];

    // Why: as with the other fields, what is not given stays, so a person can change the words without retyping the regex.
    private static TextPattern? PatternGiven(ParseResult parsed, Fields fields, TextPattern? kept)
    {
        if (parsed.GetValue(fields.NoPattern))
        {
            return null;
        }
        if (PatternOptions(fields).All(option => parsed.GetResult(option) is null))
        {
            return kept;
        }
        var example = parsed.GetValue(fields.PatternExample) ?? kept?.Example;
        return new TextPattern
        {
            Regex = parsed.GetValue(fields.PatternRegex) ?? kept?.Regex ?? "",
            Means = parsed.GetValue(fields.PatternMeans) ?? kept?.Means ?? "",
            Example = example?.Length > 0 ? example : null,
            TellCustomer = parsed.GetValue(fields.PatternTellCustomer) ?? kept?.TellCustomer ?? TellCustomers.Upfront,
        };
    }

    private static void AddFields(Command command, Fields fields)
    {
        command.Options.Add(fields.Type);
        command.Options.Add(fields.Description);
        command.Options.Add(fields.WritableBy);
        command.Options.Add(fields.Values);
        command.Options.Add(fields.Min);
        command.Options.Add(fields.Max);
        command.Options.Add(fields.MaxLength);
        foreach (var option in PatternOptions(fields))
        {
            command.Options.Add(option);
        }
        command.Options.Add(fields.NoPattern);
        command.Validators.Add(result =>
        {
            if (result.GetValue(fields.NoPattern) && PatternOptions(fields).Any(option => result.GetResult(option) is not null))
            {
                result.AddError("--no-pattern takes the pattern off, so give it without any --pattern- option.");
            }
        });
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
        var fact = answer.Body?.Deserialize<Fact>(Web) ?? throw new InvalidOperationException("the server sent no fact");
        var past = word == "add" ? "Added" : "Saved";
        var text = dry
            ? $"Would {word} '{fact.Name}' ({FactText.Constraint(fact)}). Nothing was changed."
            : string.Create(CultureInfo.InvariantCulture, $"{past} '{fact.Name}' ({FactText.Constraint(fact)}), revision {fact.Revision}.");
        return await target.Run.Report.DoneAsync(new FactSavedDocument(target.Bot, fact, dry), text);
    }
}
