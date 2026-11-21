using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.Procedures.Blocks;

namespace Suwagcms.Sdk.Validation.Rules;

internal static class ActionBlockRules
{
    public static IReadOnlyList<Rule> All { get; } =
    [
        Save(
            MistakeCode.ActionMappingTypeMismatch,
            (definition, procedure) => Mappings(definition, procedure, code: false, TypeMismatches)
        ),
        Save(MistakeCode.ActionMappingTooWide, (definition, procedure) => Mappings(definition, procedure, code: false, TooWide)),
        Save(MistakeCode.ActionMappingResultTwice, (_, procedure) => ResultsTwice(procedure)),
        Save(MistakeCode.StepActionFieldForeign, (_, procedure) => ForeignFields(procedure)),
        ProcedureMistake.For(
            MistakeCode.ActionCodeMapping,
            ValidationStage.Version,
            (definition, procedure) =>
                Mappings(definition, procedure, code: true, MissingFields)
                    .Concat(Mappings(definition, procedure, code: true, TypeMismatches))
                    .Concat(Mappings(definition, procedure, code: true, TooWide))
                    .Select(mistake => mistake with { Code = MistakeCode.ActionCodeMapping })
        ),
        ProcedureMistake.For(MistakeCode.StepActionNoOnFailure, ValidationStage.Version, (_, procedure) => NoOnFailure(procedure)),
        ProcedureMistake.For(MistakeCode.StepActionUnmappedParameter, ValidationStage.Version, Unmapped),
    ];

    public static IEnumerable<Mistake> MissingMappedFields(BotDefinition definition) =>
        definition.Procedures.SelectMany(procedure => Mappings(definition, procedure, code: false, MissingFields));

    private sealed record Mapping(Procedure Procedure, string Step, BotAction Action, string Kind, string Field, string Fact, string Path);

    private static Rule Save(MistakeCode code, Func<BotDefinition, Procedure, IEnumerable<Mistake>> check) =>
        ProcedureMistake.For(code, ValidationStage.Save, check);

    private static BotAction? ActionOf(BotDefinition definition, ProcedureStep step) =>
        step.Action is { } id ? definition.Actions.FirstOrDefault(action => action.Id == id) : null;

    private static IEnumerable<Mistake> Mappings(
        BotDefinition definition,
        Procedure procedure,
        bool code,
        Func<BotDefinition, Mapping, IEnumerable<Mistake>> check
    ) =>
        procedure
            .Steps.Where(step => ActionOf(definition, step.Value)?.IsCode == code)
            .SelectMany(step =>
            {
                var action = ActionOf(definition, step.Value) ?? throw new InvalidOperationException("the filter above found the action");
                return (step.Value.With ?? new Dictionary<string, string>())
                    .Select(entry => new Mapping(
                        procedure,
                        step.Key,
                        action,
                        "parameter",
                        entry.Key,
                        entry.Value,
                        $"steps.{step.Key}.with.{entry.Key}"
                    ))
                    .Concat(
                        (step.Value.Results ?? new Dictionary<string, string>()).Select(entry => new Mapping(
                            procedure,
                            step.Key,
                            action,
                            "result",
                            entry.Key,
                            entry.Value,
                            $"steps.{step.Key}.results.{entry.Key}"
                        ))
                    );
            })
            .SelectMany(mapping => check(definition, mapping));

    private static ActionField? FieldOf(Mapping mapping) =>
        (mapping.Kind == "parameter" ? mapping.Action.ParameterFields : mapping.Action.ResultFields).GetValueOrDefault(mapping.Field);

    private static GlobalFact? FactOf(BotDefinition definition, Mapping mapping) =>
        mapping.Procedure.Locals.TryGetValue(mapping.Fact, out var local)
            ? local.Named(mapping.Fact)
            : definition.Facts.FirstOrDefault(fact => fact.Name == mapping.Fact);

    private static IEnumerable<Mistake> MissingFields(BotDefinition definition, Mapping mapping)
    {
        if (FieldOf(mapping) is null)
        {
            var fields = mapping.Kind == "parameter" ? mapping.Action.ParameterFields : mapping.Action.ResultFields;
            var plural = mapping.Kind == "parameter" ? "Parameters" : "Results";
            yield return Make(
                mapping,
                MistakeCode.ReferenceMissing,
                $"Step '{mapping.Step}' maps {mapping.Kind} '{mapping.Field}', which action '{mapping.Action.Id}' does not have.",
                $"{plural}: {(fields.Count == 0 ? "none" : string.Join(", ", fields.Keys.Order(StringComparer.Ordinal)))}."
            );
        }
    }

    private static IEnumerable<Mistake> TypeMismatches(BotDefinition definition, Mapping mapping)
    {
        if (FieldOf(mapping) is { } field && FactOf(definition, mapping) is { } fact && field.Type != fact.Type)
        {
            yield return Make(mapping, MistakeCode.ActionMappingTypeMismatch, Passes(mapping, fact, field), "Map a fact of the same type.");
        }
    }

    private static IEnumerable<Mistake> TooWide(BotDefinition definition, Mapping mapping)
    {
        if (FieldOf(mapping) is { } field && FactOf(definition, mapping) is { } fact && field.Type == fact.Type)
        {
            var fromFact = mapping.Kind == "parameter";
            var fits = fromFact ? Fits(ToField(fact), field) : Fits(field, ToField(fact));
            if (!fits)
            {
                yield return Make(
                    mapping,
                    MistakeCode.ActionMappingTooWide,
                    Passes(mapping, fact, field),
                    fromFact
                        ? $"Use a fact that holds only what the parameter takes, or widen parameter '{mapping.Field}'."
                        : $"Use a fact that takes all the result can hold, or widen fact '{fact.Name}'."
                );
            }
        }
    }

    private static bool Fits(ActionField source, ActionField target) =>
        source.Type switch
        {
            FactTypes.Enum => (source.Values ?? []).All(value => (target.Values ?? []).Contains(value, StringComparer.Ordinal)),
            FactTypes.WholeNumber => source.Min >= target.Min && source.Max <= target.Max,
            FactTypes.Text => source.MaxLength <= target.MaxLength,
            _ => true,
        };

    private static ActionField ToField(GlobalFact fact) =>
        new()
        {
            Type = fact.Type,
            Values = fact.Values,
            Min = fact.Min,
            Max = fact.Max,
            MaxLength = fact.MaxLength,
        };

    private static string Passes(Mapping mapping, GlobalFact fact, ActionField field)
    {
        var theFact = $"fact '{fact.Name}' ({DeclarationContract.Contract(ToField(fact))})";
        var theField = $"{mapping.Kind} '{mapping.Field}' ({DeclarationContract.Contract(field)})";
        return mapping.Kind == "parameter"
            ? $"Step '{mapping.Step}' passes {theFact} to {theField}."
            : $"Step '{mapping.Step}' writes {theField} to {theFact}.";
    }

    private static IEnumerable<Mistake> ResultsTwice(Procedure procedure) =>
        procedure.Steps.SelectMany(step =>
            (step.Value.Results ?? new Dictionary<string, string>())
                .GroupBy(entry => entry.Value, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group =>
                    ProcedureMistake.Make(
                        procedure,
                        MistakeCode.ActionMappingResultTwice,
                        ValidationStage.Save,
                        $"steps.{step.Key}.results",
                        $"Step '{step.Key}' writes results {string.Join(" and ", group.Select(entry => $"'{entry.Key}'").Order(StringComparer.Ordinal))} to one fact, '{group.Key}'.",
                        "Give each result its own fact, or leave one unmapped."
                    )
                )
        );

    private static IEnumerable<Mistake> ForeignFields(Procedure procedure) =>
        procedure
            .Steps.Where(step => step.Value.Blocks.Any(block => block is not ActionBlock))
            .SelectMany(step =>
                new (string Field, bool Given)[]
                {
                    ("onFailure", step.Value.OnFailure is not null),
                    ("onUnavailable", step.Value.OnUnavailable is not null),
                    ("with", step.Value.With is not null),
                    ("results", step.Value.Results is not null),
                }
                    .Where(field => field.Given)
                    .Select(field =>
                        ProcedureMistake.Make(
                            procedure,
                            MistakeCode.StepActionFieldForeign,
                            ValidationStage.Save,
                            $"steps.{step.Key}.{field.Field}",
                            $"Step '{step.Key}' has '{field.Field}', which only an action step can have.",
                            $"Remove '{field.Field}', or make this an action step."
                        )
                    )
            );

    private static IEnumerable<Mistake> NoOnFailure(Procedure procedure) =>
        procedure
            .Steps.Where(step => step.Value.Action is not null && !ProcedureMistake.Given(step.Value.OnFailure))
            .Select(step =>
                ProcedureMistake.Make(
                    procedure,
                    MistakeCode.StepActionNoOnFailure,
                    ValidationStage.Version,
                    $"steps.{step.Key}.onFailure",
                    $"Step '{step.Key}' has no onFailure.",
                    "Add the step to go to when the action fails."
                )
            );

    private static IEnumerable<Mistake> Unmapped(BotDefinition definition, Procedure procedure) =>
        procedure.Steps.SelectMany(step =>
            ActionOf(definition, step.Value) is not { } action
                ? []
                : action
                    .ParameterFields.Keys.Where(name => step.Value.With?.ContainsKey(name) != true)
                    .Order(StringComparer.Ordinal)
                    .Select(name =>
                        ProcedureMistake.Make(
                            procedure,
                            MistakeCode.StepActionUnmappedParameter,
                            ValidationStage.Version,
                            $"steps.{step.Key}.with.{name}",
                            $"Step '{step.Key}' does not map parameter '{name}'.",
                            "Add it under 'with'."
                        )
                    )
        );

    private static Mistake Make(Mapping mapping, MistakeCode code, string message, string fix) =>
        ProcedureMistake.Make(mapping.Procedure, code, ValidationStage.Save, mapping.Path, message, fix);
}
