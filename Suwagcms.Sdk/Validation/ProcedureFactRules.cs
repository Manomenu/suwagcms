using System.Globalization;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Validation;

internal static class ProcedureFactRules
{
    public static IReadOnlyList<Rule> All { get; } =
    [
        For(MistakeCodes.FactCollectNotWritable, NotWritable),
        For(MistakeCodes.ConditionKindInvalid, Kinds),
        For(MistakeCodes.ConditionTypeMisfit, Misfits),
        For(MistakeCodes.ConditionValueInvalid, ConditionValues),
        For(MistakeCodes.AssignValueInvalid, AssignValues),
    ];

    private static Rule For(string code, Func<BotDefinition, Procedure, IEnumerable<Mistake>> check) =>
        ProcedureMistake.For(code, ValidationStage.Save, check);

    public static IEnumerable<Mistake> MissingFacts(BotDefinition definition) =>
        definition.Procedures.SelectMany(procedure => MissingFacts(definition, procedure));

    private static IEnumerable<Mistake> MissingFacts(BotDefinition definition, Procedure procedure)
    {
        var facts = definition.Facts.Select(fact => fact.Name).Concat(procedure.Locals.Keys).ToHashSet(StringComparer.Ordinal);
        var known =
            facts.Count == 0
                ? "The procedure sees no facts yet: add one first."
                : $"Facts it sees: {string.Join(", ", facts.Order(StringComparer.Ordinal))}. Local facts of other procedures are not visible.";
        foreach (var reference in ReferenceWalker.Uses(procedure).Where(reference => !facts.Contains(reference.To.Id)))
        {
            yield return Make(
                procedure,
                MistakeCodes.ReferenceMissing,
                reference.Path,
                $"{Where(reference)} in '{procedure.Id}' names fact '{reference.To.Id}', which this procedure does not see.",
                known
            );
        }
    }

    private static IEnumerable<Mistake> NotWritable(BotDefinition definition, Procedure procedure)
    {
        foreach (var reference in ReferenceWalker.Uses(procedure).Where(reference => reference.Use == FactUse.Collect))
        {
            if (Fact(definition, procedure, reference.To.Id) is { WritableBy: FactWriters.Assign })
            {
                yield return Make(
                    procedure,
                    MistakeCodes.FactCollectNotWritable,
                    reference.Path,
                    $"{Where(reference)} names fact '{reference.To.Id}', which only an assign step may write.",
                    "Remove it from collect, or give the fact 'writableBy: set'.",
                    [
                        procedure.Locals.ContainsKey(reference.To.Id)
                            ? new RelatedResource(
                                new ResourceRef(ResourceKind.Procedure, procedure.Id),
                                $"localFacts.{reference.To.Id}.writableBy"
                            )
                            : new RelatedResource(reference.To, "writableBy"),
                    ]
                );
            }
        }
    }

    private static IEnumerable<Mistake> Kinds(BotDefinition definition, Procedure procedure) =>
        Conditions(procedure)
            .Where(entry => entry.Condition.Kinds.Count != 1)
            .Select(entry =>
                Make(
                    procedure,
                    MistakeCodes.ConditionKindInvalid,
                    entry.Path,
                    $"The condition on '{entry.Fact}' has {entry.Condition.Kinds.Count} kinds of condition.",
                    "Give exactly one: oneOf, range or known."
                )
            );

    private static IEnumerable<Mistake> Misfits(BotDefinition definition, Procedure procedure)
    {
        foreach (var entry in Conditions(procedure).Where(entry => entry.Condition.Range is not null))
        {
            if (Fact(definition, procedure, entry.Fact) is { } fact && fact.Type != FactTypes.WholeNumber)
            {
                yield return Make(
                    procedure,
                    MistakeCodes.ConditionTypeMisfit,
                    $"{entry.Path}.range",
                    $"The condition 'range' does not fit {Article(fact.Type)} {fact.Type} fact '{fact.Name}'.",
                    $"Conditions for {Article(fact.Type)} {fact.Type} fact: oneOf, known."
                );
            }
        }
    }

    private static IEnumerable<Mistake> ConditionValues(BotDefinition definition, Procedure procedure)
    {
        foreach (var entry in Conditions(procedure))
        {
            if (Fact(definition, procedure, entry.Fact) is not { } fact)
            {
                continue;
            }
            if (entry.Condition.OneOf is { } values)
            {
                if (values.Count == 0)
                {
                    yield return Value(
                        procedure,
                        entry.Path + ".oneOf",
                        $"oneOf of '{fact.Name}' lists no values.",
                        "List at least one value: oneOf: [a, b]."
                    );
                }
                foreach (
                    var (value, index) in values
                        .Select((value, index) => (value, index))
                        .Where(item => !FactValues.Accepts(fact, item.value))
                )
                {
                    yield return Value(
                        procedure,
                        string.Create(CultureInfo.InvariantCulture, $"{entry.Path}.oneOf[{index}]"),
                        $"'{value}' is not a value of '{fact.Name}'.",
                        FactValues.Valid(fact)
                    );
                }
            }
            if (entry.Condition.Range is { } range && fact.Type == FactTypes.WholeNumber)
            {
                foreach (var problem in RangeProblems(fact, range))
                {
                    yield return Value(procedure, entry.Path + ".range", problem, FactValues.Valid(fact));
                }
            }
        }
    }

    private static IEnumerable<string> RangeProblems(GlobalFact fact, NumberRange range)
    {
        if (range.Min is null && range.Max is null)
        {
            yield return $"range of '{fact.Name}' has neither min nor max.";
        }
        else if (range.Min > range.Max)
        {
            yield return string.Create(CultureInfo.InvariantCulture, $"range of '{fact.Name}' has min {range.Min} above max {range.Max}.");
        }
        else if (range.Min > fact.Max || range.Max < fact.Min)
        {
            yield return string.Create(
                CultureInfo.InvariantCulture,
                $"range {range.Min}..{range.Max} of '{fact.Name}' holds no value the fact can take."
            );
        }
    }

    private static IEnumerable<Mistake> AssignValues(BotDefinition definition, Procedure procedure)
    {
        foreach (var (stepName, step) in procedure.Steps)
        {
            foreach (var (name, value) in step.Assign ?? new Dictionary<string, string>())
            {
                if (Fact(definition, procedure, name) is { } fact && !FactValues.Accepts(fact, value))
                {
                    yield return Make(
                        procedure,
                        MistakeCodes.AssignValueInvalid,
                        $"steps.{stepName}.assign.{name}",
                        $"'{value}' is not a value of '{name}'.",
                        FactValues.Valid(fact)
                    );
                }
            }
        }
    }

    private sealed record ConditionAt(string Path, string Fact, Condition Condition);

    private static IEnumerable<ConditionAt> Conditions(Procedure procedure) =>
        procedure.Steps.SelectMany(step =>
            (step.Value.Next?.Rules ?? []).SelectMany(
                (rule, index) =>
                    (rule.When ?? new Dictionary<string, Condition>()).Select(entry => new ConditionAt(
                        string.Create(CultureInfo.InvariantCulture, $"steps.{step.Key}.next[{index}].when.{entry.Key}"),
                        entry.Key,
                        entry.Value
                    ))
            )
        );

    // Why: inside a procedure a short name means its own local fact first; the name clash rule keeps that from hiding a global one.
    private static GlobalFact? Fact(BotDefinition definition, Procedure procedure, string name) =>
        procedure.Locals.TryGetValue(name, out var local) ? local.Named(name) : definition.Facts.FirstOrDefault(fact => fact.Name == name);

    private static string Where(Reference reference) =>
        reference.Use switch
        {
            FactUse.Collect => $"'collect' of step '{Step(reference)}'",
            FactUse.Assign => $"'assign' of step '{Step(reference)}'",
            FactUse.Condition => $"'when' of a rule of step '{Step(reference)}'",
            _ => throw new ArgumentOutOfRangeException(nameof(reference)),
        };

    private static string Step(Reference reference) => reference.Path.Split('.')[1];

    private static string Article(string type) => type is FactTypes.Enum or FactTypes.WholeNumber ? "an" : "a";

    private static Mistake Value(Procedure procedure, string path, string message, string fix) =>
        Make(procedure, MistakeCodes.ConditionValueInvalid, path, message, fix);

    private static Mistake Make(
        Procedure procedure,
        string code,
        string path,
        string message,
        string fix,
        IReadOnlyList<RelatedResource>? related = null
    ) => ProcedureMistake.Make(procedure, code, ValidationStage.Save, path, message, fix, related);
}
