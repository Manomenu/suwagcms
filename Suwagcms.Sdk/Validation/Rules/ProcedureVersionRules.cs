using System.Globalization;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Sdk.Validation.Rules;

internal static class ProcedureVersionRules
{
    public static IReadOnlyList<Rule> All { get; } =
    [
        For(MistakeCode.ProcedureFirstStepMissing, FirstStep),
        For(MistakeCode.StepBlockMissing, NoBlock),
        For(MistakeCode.StepBlockEmpty, EmptyBlocks),
        For(MistakeCode.StepInformEmpty, InformsNothing),
        For(MistakeCode.StepInformPointBlank, BlankPoints),
        For(MistakeCode.InstructionBlank, InformBlank),
        For(MistakeCode.StepUnreachable, Unreachable),
        For(MistakeCode.NextDefaultMissing, NoDefault),
        For(MistakeCode.NextDefaultNotLast, DefaultNotLast),
        For(MistakeCode.ProcedureNoWhenStalled, NoWhenStalled),
        For(MistakeCode.ProcedureInstructionBlank, BlankInstruction),
        new Rule(MistakeCode.BotConversationMissing, ValidationStage.Version, NoConversation),
    ];

    private static Rule For(MistakeCode code, Func<Procedure, IEnumerable<Mistake>> check) =>
        ProcedureMistake.For(code, ValidationStage.Version, (_, procedure) => check(procedure));

    private static IEnumerable<Mistake> FirstStep(Procedure procedure)
    {
        if (!ProcedureMistake.Given(procedure.FirstStep))
        {
            yield return Make(
                procedure,
                MistakeCode.ProcedureFirstStepMissing,
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
                    MistakeCode.StepBlockMissing,
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
                            MistakeCode.StepBlockEmpty,
                            $"steps.{step.Key}.{block.Name}",
                            $"In step '{step.Key}', {gap.Message}",
                            gap.Fix
                        )
                        : null
                )
                .OfType<Mistake>()
        );

    private static IEnumerable<Mistake> InformsNothing(Procedure procedure) =>
        procedure
            .Steps.Where(step => step.Value.Inform is { } inform && (inform.Points?.Count ?? 0) == 0)
            .Select(step =>
                Make(
                    procedure,
                    MistakeCode.StepInformEmpty,
                    $"steps.{step.Key}.inform",
                    $"'inform' of step '{step.Key}' has no points.",
                    "List what the customer must hear: points: { name: what must be said }."
                )
            );

    private static IEnumerable<Mistake> BlankPoints(Procedure procedure) =>
        procedure.Steps.SelectMany(step =>
            (step.Value.Inform?.Points ?? new Dictionary<string, string?>())
                .Where(point => !ProcedureMistake.Given(point.Value))
                .Select(point =>
                    Make(
                        procedure,
                        MistakeCode.StepInformPointBlank,
                        $"steps.{step.Key}.inform.points.{point.Key}",
                        $"Point '{point.Key}' of 'inform' of step '{step.Key}' has no text.",
                        "Write what the customer must hear after the point's name."
                    )
                )
        );

    private static IEnumerable<Mistake> InformBlank(Procedure procedure) =>
        procedure
            .Steps.Where(step => step.Value.Inform is { } inform && !ProcedureMistake.Given(inform.Instruction))
            .Select(step =>
                Make(
                    procedure,
                    MistakeCode.InstructionBlank,
                    $"steps.{step.Key}.inform.instruction",
                    $"'inform' of step '{step.Key}' has no instruction.",
                    "Say what the agent should do with these points."
                )
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
            var step = procedure.Steps[current];
            var targets = (step.Next?.Targets ?? []).Select(target => target.Target);
            var branches = new[] { step.OnFailure, step.OnUnavailable }.OfType<string>();
            foreach (var target in targets.Concat(branches))
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
                MistakeCode.StepUnreachable,
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
                    MistakeCode.NextDefaultMissing,
                    $"steps.{step.Key}.next",
                    $"The If of step '{step.Key}' has no Else.",
                    "Add an Else: a last branch with only 'to: <step>', taken when no condition holds."
                )
            );

    private static IEnumerable<Mistake> DefaultNotLast(Procedure procedure) =>
        procedure.Steps.SelectMany(step =>
            (step.Value.Next?.Rules ?? [])
                .SkipLast(1)
                .Select((rule, index) => (rule, index))
                .Where(item => item.rule.When is not { Count: > 0 })
                .Select(item =>
                    Make(
                        procedure,
                        MistakeCode.NextDefaultNotLast,
                        string.Create(CultureInfo.InvariantCulture, $"steps.{step.Key}.next[{item.index}]"),
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"Step '{step.Key}' has an {(item.index == 0 ? "If" : "Else if")} with no condition, so the branches after it never apply."
                        ),
                        "Choose a fact for it, or move it to the end as the Else."
                    )
                )
        );

    private static IEnumerable<Mistake> NoWhenStalled(Procedure procedure)
    {
        if (procedure.WhenStalled is null && procedure.Steps.Values.Any(step => step.Collect is not null))
        {
            yield return Make(
                procedure,
                MistakeCode.ProcedureNoWhenStalled,
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
                MistakeCode.ProcedureInstructionBlank,
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
                MistakeCode.BotConversationMissing,
                ValidationStage.Version,
                new ResourceRef(ResourceKind.Definition, string.Empty),
                "procedures",
                $"This bot has no '{Procedure.ConversationId}' procedure, the base of every conversation.",
                $"Add a procedure with the id '{Procedure.ConversationId}': one listen step whose next returns to itself, or a step that listens and one that answers, joined by next.",
                []
            );
        }
    }

    private static Mistake Make(Procedure procedure, MistakeCode code, string path, string message, string fix) =>
        ProcedureMistake.Make(procedure, code, ValidationStage.Version, path, message, fix);
}
