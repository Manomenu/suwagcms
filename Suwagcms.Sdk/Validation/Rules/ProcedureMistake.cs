using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Sdk.Validation.Rules;

internal static class ProcedureMistake
{
    public static Rule For(MistakeCode code, ValidationStage stage, Func<BotDefinition, Procedure, IEnumerable<Mistake>> check) =>
        new(
            code,
            stage,
            definition => definition.Procedures.SelectMany(procedure => check(definition, procedure).Where(mistake => mistake.Code == code))
        );

    public static Mistake Make(
        Procedure procedure,
        MistakeCode code,
        ValidationStage stage,
        string path,
        string message,
        string fix,
        IReadOnlyList<RelatedResource>? related = null
    ) => new(code, stage, new ResourceRef(ResourceKind.Procedure, procedure.Id), path, message, fix, related ?? []);

    public static string StepList(Procedure procedure) =>
        procedure.Steps.Count == 0
            ? $"'{procedure.Id}' has no steps yet: add one under steps."
            : $"Steps of '{procedure.Id}': {string.Join(", ", procedure.Steps.Keys.Order(StringComparer.Ordinal))}.";

    public static bool Given(string? text) => !string.IsNullOrWhiteSpace(text);

    public static string? ExistingFirstStep(Procedure procedure) =>
        procedure.FirstStep is { } first && Given(first) && procedure.Steps.ContainsKey(first) ? first : null;
}
