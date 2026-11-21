using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.Procedures.Blocks;

namespace Suwagcms.Sdk.Validation.Rules;

internal static class ProcedureShapeRules
{
    public static IReadOnlyList<Rule> All { get; } =
    [
        Save(MistakeCode.ProcedureIdInvalid, Id),
        Save(MistakeCode.ProcedureDescriptionMissing, Description),
        Save(MistakeCode.StepNameInvalid, StepNames),
        Save(MistakeCode.StepBlockUnknown, UnknownBlocks),
        Save(MistakeCode.StepBlockMultiple, MultipleBlocks),
        Save(MistakeCode.StepMissing, MissingSteps),
        Save(MistakeCode.StartTargetConversation, StartsConversation),
        Save(MistakeCode.StepInformPointName, PointNames),
        Save(MistakeCode.ProcedureWhenInterruptedInvalid, WhenInterruptedValue),
        Save(MistakeCode.ProcedureWhenInterruptedOnConversation, WhenInterruptedOnConversation),
    ];

    private static Rule Save(MistakeCode code, Func<Procedure, IEnumerable<Mistake>> check) =>
        ProcedureMistake.For(code, ValidationStage.Save, (_, procedure) => check(procedure));

    private static IEnumerable<Mistake> Id(Procedure procedure)
    {
        if (!ResourceIds.IsCamelCase(procedure.Id))
        {
            yield return Make(
                procedure,
                MistakeCode.ProcedureIdInvalid,
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
                MistakeCode.ProcedureDescriptionMissing,
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
                    MistakeCode.StepNameInvalid,
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
                        MistakeCode.StepBlockUnknown,
                        $"steps.{step.Key}.{key}",
                        $"Step '{step.Key}' has the unknown block '{key}'.",
                        key == "knowledge"
                            ? "Knowledge belongs on the instruction of an inform: move it there."
                            : $"Use one of the blocks: {string.Join(", ", Block.Names)}; next says where to go."
                    )
                )
        );

    private static IEnumerable<Mistake> MultipleBlocks(Procedure procedure) =>
        procedure
            .Steps.Where(step => step.Value.Blocks.Count > 1)
            .Select(step =>
                Make(
                    procedure,
                    MistakeCode.StepBlockMultiple,
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
            if (step.OnFailure is { } failure && ProcedureMistake.Given(failure) && !procedure.Steps.ContainsKey(failure))
            {
                yield return Missing(procedure, $"steps.{name}.onFailure", $"'onFailure: {failure}' of step '{name}'");
            }
            if (step.OnUnavailable is { } unavailable && ProcedureMistake.Given(unavailable) && !procedure.Steps.ContainsKey(unavailable))
            {
                yield return Missing(procedure, $"steps.{name}.onUnavailable", $"'onUnavailable: {unavailable}' of step '{name}'");
            }
            foreach (var (path, target) in step.Next?.Targets ?? [])
            {
                if (!procedure.Steps.ContainsKey(target))
                {
                    yield return string.IsNullOrWhiteSpace(target)
                        ? Make(
                            procedure,
                            MistakeCode.StepMissing,
                            $"steps.{name}.{path}",
                            $"A branch of step '{name}' goes to no step.",
                            "Add a step to this branch, or a Next: row."
                        )
                        : Missing(procedure, $"steps.{name}.{path}", $"'{path.Split('.')[^1]}: {target}' of step '{name}'");
                }
            }
        }
    }

    private static IEnumerable<Mistake> StartsConversation(Procedure procedure) =>
        procedure
            .Steps.Where(step => step.Value.Start == Procedure.ConversationId)
            .Select(step =>
                Make(
                    procedure,
                    MistakeCode.StartTargetConversation,
                    $"steps.{step.Key}.start",
                    $"'start' of step '{step.Key}' in '{procedure.Id}' names '{Procedure.ConversationId}', which cannot be started: it is the base of every conversation.",
                    "End the procedure here instead, by leaving out 'next'."
                )
            );

    private static IEnumerable<Mistake> PointNames(Procedure procedure) =>
        procedure.Steps.SelectMany(step =>
            (step.Value.Inform?.Points?.Keys ?? [])
                .Where(name => !ResourceIds.IsCamelCase(name))
                .Select(name =>
                    Make(
                        procedure,
                        MistakeCode.StepInformPointName,
                        $"steps.{step.Key}.inform.points.{name}",
                        $"The point name '{name}' in 'inform' of step '{step.Key}' is not camelCase.",
                        $"Rename it to '{ResourceIds.Suggest(name)}'."
                    )
                )
        );

    private static IEnumerable<Mistake> WhenInterruptedValue(Procedure procedure)
    {
        if (procedure.WhenInterrupted is { } value && !ProcedureInterruptions.All.Contains(value, StringComparer.Ordinal))
        {
            yield return Make(
                procedure,
                MistakeCode.ProcedureWhenInterruptedInvalid,
                "whenInterrupted",
                $"'{procedure.Id}' has the unknown whenInterrupted '{value}'.",
                $"Use one of: {string.Join(", ", ProcedureInterruptions.All)}."
            );
        }
    }

    private static IEnumerable<Mistake> WhenInterruptedOnConversation(Procedure procedure)
    {
        if (procedure.Id == Procedure.ConversationId && procedure.WhenInterrupted is not null)
        {
            yield return Make(
                procedure,
                MistakeCode.ProcedureWhenInterruptedOnConversation,
                "whenInterrupted",
                $"'{Procedure.ConversationId}' has whenInterrupted, but it is the base of every conversation and nothing can interrupt it away.",
                "Remove whenInterrupted from it."
            );
        }
    }

    private static Mistake Missing(Procedure procedure, string path, string what) =>
        Make(procedure, MistakeCode.StepMissing, path, $"{what} names no step of '{procedure.Id}'.", ProcedureMistake.StepList(procedure));

    private static Mistake Make(Procedure procedure, MistakeCode code, string path, string message, string fix) =>
        ProcedureMistake.Make(procedure, code, ValidationStage.Save, path, message, fix);
}
