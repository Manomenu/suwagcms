using System.Globalization;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Sdk.Validation.Rules;

internal static class FactRules
{
    private const string ReservedValue = "other";

    public static IReadOnlyList<Rule> All { get; } =
    [
        new(
            MistakeCodes.FactNameInvalid,
            ValidationStage.Save,
            definition =>
                definition
                    .Facts.SelectMany(Name)
                    .Concat(definition.Procedures.SelectMany(LocalNames))
                    .Concat(definition.Actions.Where(action => !action.IsCode).SelectMany(FieldNames))
        ),
        For(MistakeCodes.FactTypeInvalid, Type),
        For(MistakeCodes.FactDescriptionMissing, Description),
        For(MistakeCodes.FactWritableByInvalid, WritableBy),
        For(MistakeCodes.FactEnumTooFewValues, TooFewValues),
        For(MistakeCodes.FactEnumValueBlank, BlankValues),
        For(MistakeCodes.FactEnumValueRepeated, RepeatedValues),
        For(MistakeCodes.FactEnumValueUnusable, UnusableValues),
        For(MistakeCodes.FactEnumValueReserved, ReservedValues),
        For(MistakeCodes.FactIntBoundMissing, MissingBounds),
        For(MistakeCodes.FactIntRangeInverted, InvertedRange),
        For(MistakeCodes.FactTextMaxLengthInvalid, MaxLength),
        For(MistakeCodes.FactPatternRegexMissing, FactPatternRules.RegexMissing),
        For(MistakeCodes.FactPatternRegexInvalid, FactPatternRules.RegexInvalid),
        For(MistakeCodes.FactPatternRegexMatchesEmpty, FactPatternRules.RegexMatchesEmpty),
        For(MistakeCodes.FactPatternMeansMissing, FactPatternRules.MeansMissing),
        For(MistakeCodes.FactPatternExampleBlank, FactPatternRules.ExampleBlank),
        For(MistakeCodes.FactPatternExampleMismatch, FactPatternRules.ExampleMismatch),
        For(MistakeCodes.FactPatternExampleTooLong, FactPatternRules.ExampleTooLong),
        For(MistakeCodes.FactPatternTellCustomerInvalid, FactPatternRules.TellCustomerInvalid),
        For(MistakeCodes.FactConstraintForeign, ForeignConstraints),
    ];

    private static Rule For(string code, Func<GlobalFact, IEnumerable<Mistake>> check) =>
        new(
            code,
            ValidationStage.Save,
            definition => definition.Facts.SelectMany(check).Concat(Locals(definition, check)).Concat(ActionFields(definition, check))
        );

    private static IEnumerable<Mistake> ActionFields(BotDefinition definition, Func<GlobalFact, IEnumerable<Mistake>> check) =>
        definition
            .Actions.Where(action => !action.IsCode)
            .SelectMany(action =>
                FieldsOf(action)
                    .SelectMany(field =>
                        check(field.Field.Named(field.Name)).Select(mistake => InAction(mistake, action, $"{field.List}.{field.Name}"))
                    )
            );

    private static IEnumerable<(string List, string Name, ActionField Field)> FieldsOf(BotAction action) =>
        action
            .ParameterFields.Select(entry => ("parameters", entry.Key, entry.Value))
            .Concat(action.ResultFields.Select(entry => ("results", entry.Key, entry.Value)));

    private static Mistake InAction(Mistake mistake, BotAction action, string place)
    {
        var resource = new ResourceRef(ResourceKind.Action, action.Id);
        return mistake with
        {
            Resource = resource,
            Path = $"{place}.{mistake.Path}",
            Related = [.. mistake.Related.Select(related => new RelatedResource(resource, $"{place}.{related.Location}"))],
        };
    }

    private static IEnumerable<Mistake> FieldNames(BotAction action) =>
        FieldsOf(action)
            .Where(field => !ResourceIds.IsCamelCase(field.Name))
            .Select(field => new Mistake(
                MistakeCodes.FactNameInvalid,
                ValidationStage.Save,
                new ResourceRef(ResourceKind.Action, action.Id),
                $"{field.List}.{field.Name}",
                $"'{field.Name}' is not a name for this list: it is one camelCase word with no dots (visitDate).",
                $"Rename it to '{ResourceIds.Suggest(field.Name)}'.",
                []
            ));

    private static IEnumerable<Mistake> Locals(BotDefinition definition, Func<GlobalFact, IEnumerable<Mistake>> check) =>
        definition.Procedures.SelectMany(procedure =>
            procedure.Locals.SelectMany(local =>
                check(local.Value.Named(local.Key)).Select(mistake => InProcedure(mistake, procedure, local.Key))
            )
        );

    private static Mistake InProcedure(Mistake mistake, Procedure procedure, string name)
    {
        var resource = new ResourceRef(ResourceKind.Procedure, procedure.Id);
        return mistake with
        {
            Resource = resource,
            Path = $"localFacts.{name}.{mistake.Path}",
            Related = [.. mistake.Related.Select(related => new RelatedResource(resource, $"localFacts.{name}.{related.Location}"))],
        };
    }

    private static IEnumerable<Mistake> Name(GlobalFact fact)
    {
        if (!FactNames.IsValid(fact.Name))
        {
            yield return Make(
                MistakeCodes.FactNameInvalid,
                fact,
                "name",
                $"'{fact.Name}' is not a fact name: it is camelCase words, which may be joined by dots (customer.city).",
                $"Rename the fact to '{FactNames.Suggest(fact.Name)}'."
            );
        }
    }

    private static IEnumerable<Mistake> LocalNames(Procedure procedure) =>
        procedure
            .Locals.Keys.Where(name => !ResourceIds.IsCamelCase(name))
            .Select(name => new Mistake(
                MistakeCodes.FactNameInvalid,
                ValidationStage.Save,
                new ResourceRef(ResourceKind.Procedure, procedure.Id),
                $"localFacts.{name}",
                $"'{name}' is not a local fact name: it is one camelCase word with no dots (visitDate).",
                $"Rename the local fact to '{ResourceIds.Suggest(name)}'.",
                []
            ));

    private static IEnumerable<Mistake> Type(GlobalFact fact)
    {
        if (!FactTypes.All.Contains(fact.Type, StringComparer.Ordinal))
        {
            yield return Make(
                MistakeCodes.FactTypeInvalid,
                fact,
                "type",
                string.IsNullOrWhiteSpace(fact.Type) ? $"'{fact.Name}' has no type." : $"'{fact.Name}' has the unknown type '{fact.Type}'.",
                $"Set the type to one of: {string.Join(", ", FactTypes.All)}."
            );
        }
    }

    private static IEnumerable<Mistake> Description(GlobalFact fact)
    {
        if (string.IsNullOrWhiteSpace(fact.Description))
        {
            yield return Make(
                MistakeCodes.FactDescriptionMissing,
                fact,
                "description",
                $"'{fact.Name}' has no description.",
                "Add one: the model reads it to understand what the fact means."
            );
        }
    }

    private static IEnumerable<Mistake> WritableBy(GlobalFact fact)
    {
        if (!FactWriters.All.Contains(fact.WritableBy, StringComparer.Ordinal))
        {
            yield return Make(
                MistakeCodes.FactWritableByInvalid,
                fact,
                "writableBy",
                $"'{fact.Name}' has the unknown writableBy '{fact.WritableBy}'.",
                $"Use one of: {string.Join(", ", FactWriters.All)}."
            );
        }
    }

    private static IEnumerable<Mistake> TooFewValues(GlobalFact fact)
    {
        var count = fact.Values?.Count ?? 0;
        if (fact.Type == FactTypes.Enum && count < 2)
        {
            yield return Make(
                MistakeCodes.FactEnumTooFewValues,
                fact,
                "values",
                string.Create(CultureInfo.InvariantCulture, $"'{fact.Name}' has {count} value{(count == 1 ? "" : "s")}."),
                "An enum needs at least two values: add another."
            );
        }
    }

    private static IEnumerable<Mistake> BlankValues(GlobalFact fact) =>
        fact.Type != FactTypes.Enum
            ? []
            : (fact.Values ?? [])
                .Select((value, index) => (value, index))
                .Where(entry => string.IsNullOrWhiteSpace(entry.value))
                .Select(entry =>
                    Make(
                        MistakeCodes.FactEnumValueBlank,
                        fact,
                        Value(entry.index),
                        $"Value {entry.index + 1} of '{fact.Name}' is blank.",
                        "Write the value, or remove it."
                    )
                );

    private static IEnumerable<Mistake> RepeatedValues(GlobalFact fact) =>
        fact.Type != FactTypes.Enum
            ? []
            : (fact.Values ?? [])
                .Select((value, index) => (value, index))
                .Where(entry => !string.IsNullOrWhiteSpace(entry.value))
                .GroupBy(entry => entry.value, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .SelectMany(group =>
                    group
                        .Skip(1)
                        .Select(entry =>
                            Make(
                                MistakeCodes.FactEnumValueRepeated,
                                fact,
                                Value(entry.index),
                                $"The value '{entry.value}' of '{fact.Name}' is listed more than once.",
                                $"Remove the repeated '{entry.value}'.",
                                [new RelatedResource(Resource(fact), Value(group.First().index))]
                            )
                        )
                );

    private static IEnumerable<Mistake> UnusableValues(GlobalFact fact) =>
        fact.Type != FactTypes.Enum
            ? []
            : (fact.Values ?? [])
                .Select((value, index) => (value, index))
                .Where(entry => !string.IsNullOrWhiteSpace(entry.value) && entry.value.AsSpan().IndexOfAny('\n', '\r', '\0') >= 0)
                .Select(entry =>
                    Make(
                        MistakeCodes.FactEnumValueUnusable,
                        fact,
                        Value(entry.index),
                        $"Value {entry.index + 1} of '{fact.Name}' has a line break or a NUL.",
                        "Write each value as plain text on one line."
                    )
                );

    private static IEnumerable<Mistake> ReservedValues(GlobalFact fact) =>
        fact.Type != FactTypes.Enum
            ? []
            : (fact.Values ?? [])
                .Select((value, index) => (value, index))
                .Where(entry => entry.value == ReservedValue)
                .Select(entry =>
                    Make(
                        MistakeCodes.FactEnumValueReserved,
                        fact,
                        Value(entry.index),
                        $"'{fact.Name}' lists '{ReservedValue}' as a value, which is reserved for a value outside the list.",
                        $"Remove '{ReservedValue}', or use another word."
                    )
                );

    private static IEnumerable<Mistake> MissingBounds(GlobalFact fact)
    {
        if (fact.Type != FactTypes.WholeNumber)
        {
            yield break;
        }
        foreach (var (bound, name) in new[] { (fact.Min, "min"), (fact.Max, "max") })
        {
            if (bound is null)
            {
                yield return Make(
                    MistakeCodes.FactIntBoundMissing,
                    fact,
                    name,
                    $"'{fact.Name}' is an int without {name}.",
                    $"Set {name} to a whole number: an int needs both min and max."
                );
            }
        }
    }

    private static IEnumerable<Mistake> InvertedRange(GlobalFact fact)
    {
        if (fact.Type == FactTypes.WholeNumber && fact.Min > fact.Max)
        {
            yield return Make(
                MistakeCodes.FactIntRangeInverted,
                fact,
                "min",
                string.Create(CultureInfo.InvariantCulture, $"'{fact.Name}' has min {fact.Min} above max {fact.Max}."),
                "Make min at most max."
            );
        }
    }

    private static IEnumerable<Mistake> MaxLength(GlobalFact fact)
    {
        if (fact.Type == FactTypes.Text && fact.MaxLength is null or <= 0)
        {
            yield return Make(
                MistakeCodes.FactTextMaxLengthInvalid,
                fact,
                "maxLength",
                fact.MaxLength is null
                    ? $"'{fact.Name}' is a text without maxLength."
                    : string.Create(CultureInfo.InvariantCulture, $"'{fact.Name}' has maxLength {fact.MaxLength}, which is not above 0."),
                "Set maxLength to a whole number above 0."
            );
        }
    }

    private static IEnumerable<Mistake> ForeignConstraints(GlobalFact fact)
    {
        if (!FactTypes.All.Contains(fact.Type, StringComparer.Ordinal))
        {
            yield break;
        }
        var constraints = new (bool Present, string Name, string Owner)[]
        {
            (fact.Values is not null, "values", FactTypes.Enum),
            (fact.Min is not null, "min", FactTypes.WholeNumber),
            (fact.Max is not null, "max", FactTypes.WholeNumber),
            (fact.MaxLength is not null, "maxLength", FactTypes.Text),
            (fact.Pattern is not null, "pattern", FactTypes.Text),
        };
        foreach (var (present, name, owner) in constraints)
        {
            if (present && fact.Type != owner)
            {
                yield return Make(
                    MistakeCodes.FactConstraintForeign,
                    fact,
                    name,
                    $"'{fact.Name}' is {Article(fact.Type)} {fact.Type} and cannot have {name}: it belongs to {Article(owner)} {owner}.",
                    $"Remove {name}, or change the type to {owner}."
                );
            }
        }
    }

    private static string Article(string type) => type is FactTypes.Enum or FactTypes.WholeNumber ? "an" : "a";

    private static string Value(int index) => string.Create(CultureInfo.InvariantCulture, $"values[{index}]");

    private static ResourceRef Resource(GlobalFact fact) => new(ResourceKind.GlobalFact, fact.Name);

    internal static Mistake Make(
        string code,
        GlobalFact fact,
        string path,
        string message,
        string fix,
        IReadOnlyList<RelatedResource>? related = null
    ) => new(code, ValidationStage.Save, Resource(fact), path, message, fix, related ?? []);
}
