using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Validation;

internal static class ProcedureVersionRules
{
    public static IReadOnlyList<Rule> All { get; } =
    [
        For(MistakeCodes.ProcedureFirstStepMissing, FirstStep),
        For(MistakeCodes.StepBlockMissing, NoBlock),
        For(MistakeCodes.StepBlockEmpty, EmptyBlocks),
        For(MistakeCodes.StepUnreachable, Unreachable),
        For(MistakeCodes.NextDefaultMissing, NoDefault),
        For(MistakeCodes.ProcedureNoWhenStalled, NoWhenStalled),
        For(MistakeCodes.ProcedureInstructionBlank, BlankInstruction),
        new Rule(MistakeCodes.BotConversationMissing, ValidationStage.Version, NoConversation),
    ];

    private static Rule For(string code, Func<Procedure, IEnumerable<Mistake>> check) =>
        ProcedureMistake.For(code, ValidationStage.Version, (_, procedure) => check(procedure));

    private static IEnumerable<Mistake> FirstStep(Procedure procedure)
    {
        if (!ProcedureMistake.Given(procedure.FirstStep))
        {
            yield return Make(
                procedure,
                MistakeCodes.ProcedureFirstStepMissing,
                "firstStep",
                $"'{procedure.Id}' has no firstStep.",
                $"Add 'firstStep: <step>'. {ProcedureMistake.StepList(procedure)}"
            );
        }
    }

    private static IEnumerable<Mistake> NoBlock(Procedure procedure) =>
        procedure
            .Steps.Where(step => step.Value.Blocks.Count == 0 && (step.Value.UnknownBlocks?.Count ?? 0) == 0)
            .Select(step =>
                Make(
                    procedure,
                    MistakeCodes.StepBlockMissing,
                    $"steps.{step.Key}",
                    $"Step '{step.Key}' has no block.",
                    $"Give it exactly one block: {string.Join(", ", Block.Names)}."
                )
            );

    private static IEnumerable<Mistake> EmptyBlocks(Procedure procedure) =>
        procedure.Steps.SelectMany(step =>
            step.Value.Blocks.Select(block =>
                    block.Gap is { } gap
                        ? Make(
                            procedure,
                            MistakeCodes.StepBlockEmpty,
                            $"steps.{step.Key}.{block.Name}",
                            $"In step '{step.Key}', {gap.Message}",
                            gap.Fix
                        )
                        : null
                )
                .OfType<Mistake>()
        );

    private static IEnumerable<Mistake> Unreachable(Procedure procedure)
    {
        if (ProcedureMistake.ExistingFirstStep(procedure) is not { } first)
        {
            yield break;
        }
        var reached = new HashSet<string>(StringComparer.Ordinal) { first };
        var queue = new Queue<string>([first]);
        while (queue.TryDequeue(out var current))
        {
            foreach (var (_, target) in procedure.Steps[current].Next?.Targets ?? [])
            {
                if (procedure.Steps.ContainsKey(target) && reached.Add(target))
                {
                    queue.Enqueue(target);
                }
            }
        }
        foreach (var name in procedure.Steps.Keys.Where(name => !reached.Contains(name)).Order(StringComparer.Ordinal))
        {
            yield return Make(
                procedure,
                MistakeCodes.StepUnreachable,
                $"steps.{name}",
                $"No path from firstStep reaches step '{name}'.",
                "Connect it from the next of a step that is reached, or delete it."
            );
        }
    }

    private static IEnumerable<Mistake> NoDefault(Procedure procedure) =>
        procedure
            .Steps.Where(step => step.Value.Next?.Rules is { } rules && (rules.Count == 0 || rules[^1].When is { Count: > 0 }))
            .Select(step =>
                Make(
                    procedure,
                    MistakeCodes.NextDefaultMissing,
                    $"steps.{step.Key}.next",
                    $"The rules of step '{step.Key}' have no default rule.",
                    "Add a last rule with only 'to: <step>', taken when no other rule holds."
                )
            );

    private static IEnumerable<Mistake> NoWhenStalled(Procedure procedure)
    {
        if (procedure.WhenStalled is null && procedure.Steps.Values.Any(step => step.Collect is not null))
        {
            yield return Make(
                procedure,
                MistakeCodes.ProcedureNoWhenStalled,
                "whenStalled",
                $"'{procedure.Id}' has a collect step and no whenStalled.",
                "Add whenStalled with an instruction saying what the agent does when the customer gets stuck."
            );
        }
    }

    private static IEnumerable<Mistake> BlankInstruction(Procedure procedure)
    {
        if (procedure.WhenStalled is { } stalled && !ProcedureMistake.Given(stalled.Instruction))
        {
            yield return Make(
                procedure,
                MistakeCodes.ProcedureInstructionBlank,
                "whenStalled.instruction",
                $"The whenStalled of '{procedure.Id}' has a blank instruction.",
                "Write what the agent does when the customer gets stuck."
            );
        }
    }

    private static IEnumerable<Mistake> NoConversation(BotDefinition definition)
    {
        if (!definition.Procedures.Any(procedure => procedure.Id == Procedure.ConversationId))
        {
            yield return new Mistake(
                MistakeCodes.BotConversationMissing,
                ValidationStage.Version,
                new ResourceRef(ResourceKind.Definition, string.Empty),
                "procedures",
                $"This bot has no '{Procedure.ConversationId}' procedure, the base of every conversation.",
                $"Add a procedure with the id '{Procedure.ConversationId}': a step that listens and one that answers, joined by next.",
                []
            );
        }
    }

    private static Mistake Make(Procedure procedure, string code, string path, string message, string fix) =>
        ProcedureMistake.Make(procedure, code, ValidationStage.Version, path, message, fix);
}
