using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Validation;

internal static class ProcedureShapeRules
{
    public static IReadOnlyList<Rule> All { get; } =
    [
        Save(MistakeCodes.ProcedureIdInvalid, Id),
        Save(MistakeCodes.ProcedureDescriptionMissing, Description),
        Save(MistakeCodes.StepNameInvalid, StepNames),
        Save(MistakeCodes.StepBlockUnknown, UnknownBlocks),
        Save(MistakeCodes.StepBlockMultiple, MultipleBlocks),
        Save(MistakeCodes.StepMissing, MissingSteps),
    ];

    private static Rule Save(string code, Func<Procedure, IEnumerable<Mistake>> check) =>
        ProcedureMistake.For(code, ValidationStage.Save, (_, procedure) => check(procedure));

    private static IEnumerable<Mistake> Id(Procedure procedure)
    {
        if (!ResourceIds.IsCamelCase(procedure.Id))
        {
            yield return Make(
                procedure,
                MistakeCodes.ProcedureIdInvalid,
                "id",
                $"'{procedure.Id}' is not a procedure id: it is one camelCase word.",
                $"Rename the procedure to '{ResourceIds.Suggest(procedure.Id)}'."
            );
        }
    }

    private static IEnumerable<Mistake> Description(Procedure procedure)
    {
        if (!ProcedureMistake.Given(procedure.Description))
        {
            yield return Make(
                procedure,
                MistakeCodes.ProcedureDescriptionMissing,
                "description",
                $"'{procedure.Id}' has no description.",
                "Add one saying when the procedure is used: the model reads it to decide whether to start it."
            );
        }
    }

    private static IEnumerable<Mistake> StepNames(Procedure procedure) =>
        procedure
            .Steps.Keys.Where(name => !ResourceIds.IsCamelCase(name))
            .Select(name =>
                Make(
                    procedure,
                    MistakeCodes.StepNameInvalid,
                    $"steps.{name}",
                    $"The step name '{name}' is not camelCase.",
                    $"Rename the step to '{ResourceIds.Suggest(name)}'."
                )
            );

    private static IEnumerable<Mistake> UnknownBlocks(Procedure procedure) =>
        procedure.Steps.SelectMany(step =>
            (step.Value.UnknownBlocks?.Keys ?? [])
                .Order(StringComparer.Ordinal)
                .Select(key =>
                    Make(
                        procedure,
                        MistakeCodes.StepBlockUnknown,
                        $"steps.{step.Key}.{key}",
                        $"Step '{step.Key}' has the unknown block '{key}'.",
                        $"Use one of the blocks: {string.Join(", ", Block.Names)}; next says where to go."
                    )
                )
        );

    private static IEnumerable<Mistake> MultipleBlocks(Procedure procedure) =>
        procedure
            .Steps.Where(step => step.Value.Blocks.Count > 1)
            .Select(step =>
                Make(
                    procedure,
                    MistakeCodes.StepBlockMultiple,
                    $"steps.{step.Key}",
                    $"Step '{step.Key}' has {step.Value.Blocks.Count} blocks: {string.Join(", ", step.Value.Blocks.Select(block => block.Name))}.",
                    $"Keep one block per step ({string.Join(", ", Block.Names)}): split it into steps joined by next."
                )
            );

    private static IEnumerable<Mistake> MissingSteps(Procedure procedure)
    {
        if (procedure.FirstStep is { } first && ProcedureMistake.Given(first) && !procedure.Steps.ContainsKey(first))
        {
            yield return Missing(procedure, "firstStep", $"'firstStep: {procedure.FirstStep}'");
        }
        foreach (var (name, step) in procedure.Steps)
        {
            foreach (var (path, target) in step.Next?.Targets ?? [])
            {
                if (!procedure.Steps.ContainsKey(target))
                {
                    yield return Missing(
                        procedure,
                        $"steps.{name}.{path}",
                        string.IsNullOrWhiteSpace(target)
                            ? $"A rule of step '{name}' has no 'to', it"
                            : $"'{path.Split('.')[^1]}: {target}' of step '{name}'"
                    );
                }
            }
        }
    }

    private static Mistake Missing(Procedure procedure, string path, string what) =>
        Make(procedure, MistakeCodes.StepMissing, path, $"{what} names no step of '{procedure.Id}'.", ProcedureMistake.StepList(procedure));

    private static Mistake Make(Procedure procedure, string code, string path, string message, string fix) =>
        ProcedureMistake.Make(procedure, code, ValidationStage.Save, path, message, fix);
}
