using System.Globalization;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation.Rules;

namespace Suwagcms.Sdk.Validation.Guards;

public static class EditGuard
{
    public static IReadOnlyList<Mistake> Reshape(BotDefinition before, BotDefinition after, IReadOnlyList<Mistake> mistakes)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(mistakes);
        var kept = mistakes.ToList();
        var causes = new List<Mistake>();
        foreach (var procedure in after.Procedures)
        {
            var was = before.Procedures.FirstOrDefault(other => other.Id == procedure.Id);
            if (was is not null)
            {
                foreach (var gone in was.Locals.Keys.Where(name => !procedure.Locals.ContainsKey(name)).Order(StringComparer.Ordinal))
                {
                    RemovedLocal(procedure, gone, kept, causes);
                }
            }
        }
        foreach (var fact in after.Facts)
        {
            if (before.Facts.FirstOrDefault(other => other.Name == fact.Name) is { } was && Differs(was, fact))
            {
                var uses = after.Procedures.SelectMany(procedure =>
                    ReferenceWalker
                        .From(procedure)
                        .Where(use => use.To == new ResourceRef(ResourceKind.GlobalFact, fact.Name))
                        .Select(use => new FactUseIn(procedure, use))
                );
                Edited(new FactEdit(was, fact, new ResourceRef(ResourceKind.GlobalFact, fact.Name), "", [.. uses]), kept, causes);
            }
        }
        foreach (var procedure in after.Procedures)
        {
            var known = before.Procedures.FirstOrDefault(other => other.Id == procedure.Id);
            foreach (var (name, local) in procedure.Locals)
            {
                if (known is not null && known.Locals.TryGetValue(name, out var was) && Differs(was.Named(name), local.Named(name)))
                {
                    var uses = ReferenceWalker
                        .Uses(procedure)
                        .Where(use => use.To == new ResourceRef(ResourceKind.GlobalFact, name))
                        .Select(use => new FactUseIn(procedure, use));
                    Edited(
                        new FactEdit(
                            was.Named(name),
                            local.Named(name),
                            new ResourceRef(ResourceKind.Procedure, procedure.Id),
                            $"localFacts.{name}.",
                            [.. uses]
                        ),
                        kept,
                        causes
                    );
                }
            }
        }
        foreach (var action in after.Actions.Where(action => !action.IsCode))
        {
            if (before.Actions.FirstOrDefault(other => other.Id == action.Id) is { } was)
            {
                ActionFieldsGone(after, was, action, kept, causes);
            }
        }
        return [.. kept, .. causes];
    }

    private static void ActionFieldsGone(BotDefinition after, BotAction was, BotAction action, List<Mistake> kept, List<Mistake> causes)
    {
        foreach (
            var (noun, section, old, now) in new[]
            {
                ("Parameter", "parameters", was.ParameterFields, action.ParameterFields),
                ("Result", "results", was.ResultFields, action.ResultFields),
            }
        )
        {
            foreach (var (name, field) in old)
            {
                now.TryGetValue(name, out var left);
                if (left is not null && left.Type == field.Type)
                {
                    continue;
                }
                var maps = section == "parameters" ? "with" : "results";
                var places = after
                    .Procedures.SelectMany(procedure =>
                        procedure
                            .Steps.Where(step =>
                                step.Value.Action == action.Id
                                && (maps == "with" ? step.Value.With : step.Value.Results)?.ContainsKey(name) == true
                            )
                            .Select(step => (procedure, step.Key, Path: $"steps.{step.Key}.{maps}.{name}"))
                    )
                    .OrderBy(place => place.procedure.Id, StringComparer.Ordinal)
                    .ThenBy(place => place.Key, StringComparer.Ordinal)
                    .ToList();
                if (places.Count == 0)
                {
                    continue;
                }
                kept.RemoveAll(mistake => places.Any(place => mistake.Resource == Resource(place.procedure) && mistake.Path == place.Path));
                var change = left is null ? "removed" : $"changed from {field.Type} to {left.Type}";
                causes.Add(
                    new Mistake(
                        MistakeCode.ActionFieldInUse,
                        ValidationStage.Save,
                        new ResourceRef(ResourceKind.Action, action.Id),
                        $"{section}.{name}",
                        $"{noun} '{name}' of '{action.Id}' is mapped, so it cannot be {change}. Used by: {string.Join(", ", places.Select(place => $"procedure {place.procedure.Id}, step {place.Key} ({maps})"))}.",
                        "Change these steps first, or add a new one beside it.",
                        [.. places.Select(place => new RelatedResource(Resource(place.procedure), place.Path))]
                    )
                );
            }
        }
    }

    private sealed record FactUseIn(Procedure Procedure, Reference Use);

    private sealed record FactEdit(GlobalFact Was, GlobalFact Now, ResourceRef Owner, string Prefix, IReadOnlyList<FactUseIn> Uses);

    private sealed record Given(Procedure Procedure, Reference Use, string Path, MistakeCode? Code, string Value);

    private static bool Differs(GlobalFact was, GlobalFact now) =>
        was.Type != now.Type
        || was.WritableBy != now.WritableBy
        || was.Min != now.Min
        || was.Max != now.Max
        || was.MaxLength != now.MaxLength
        || was.Pattern != now.Pattern
        || !(was.Values ?? []).SequenceEqual(now.Values ?? [], StringComparer.Ordinal);

    private static void Edited(FactEdit edit, List<Mistake> kept, List<Mistake> causes)
    {
        if (edit.Was.Type != edit.Now.Type)
        {
            TypeChanged(edit, kept, causes);
            return;
        }
        NoLongerWritable(edit, kept, causes);
        var broken = edit
            .Uses.SelectMany(Values)
            .Where(given => FactValues.Accepts(edit.Was, given.Value) && !FactValues.Accepts(edit.Now, given.Value))
            .Where(given => kept.Any(mistake => IsAt(mistake, given.Code, Resource(given.Procedure), given.Path)))
            .ToList();
        kept.RemoveAll(mistake => broken.Any(given => IsAt(mistake, given.Code, Resource(given.Procedure), given.Path)));
        causes.AddRange(
            edit.Now.Type switch
            {
                FactTypes.Enum => broken
                    .GroupBy(given => given.Value, StringComparer.Ordinal)
                    .Select(group =>
                        Cause(
                            MistakeCode.FactEnumValueInUse,
                            edit,
                            "values",
                            $"Value '{group.Key}' of '{edit.Now.Name}' is used, so it cannot be removed.",
                            "Change these steps first, or keep the value.",
                            group
                        )
                    ),
                FactTypes.WholeNumber => Narrowed(edit, broken),
                FactTypes.Text => TextNarrowed(edit, broken),
                _ => [],
            }
        );
    }

    private static IEnumerable<Mistake> TextNarrowed(FactEdit edit, List<Given> broken)
    {
        var tooLong = broken.Where(given => given.Value.EnumerateRunes().Count() > edit.Now.MaxLength).ToList();
        if (tooLong.Count > 0)
        {
            yield return Cause(
                MistakeCode.FactRangeInUse,
                edit,
                "maxLength",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"'{edit.Now.Name}' cannot have maxLength {edit.Now.MaxLength}: a longer value is used."
                ),
                "Change these steps first, or keep the length.",
                tooLong
            );
        }
        var unmatched = broken.Except(tooLong).ToList();
        if (unmatched.Count > 0)
        {
            var values = string.Join(", ", unmatched.Select(given => $"'{given.Value}'").Distinct(StringComparer.Ordinal));
            yield return Cause(
                MistakeCode.FactPatternInUse,
                edit,
                "pattern.regex",
                $"'{edit.Now.Name}' cannot have the regex {edit.Now.Pattern?.Regex}: {values} is used and does not match it.",
                "Change these steps first, or keep the pattern.",
                unmatched
            );
        }
    }

    private static IEnumerable<Mistake> Narrowed(FactEdit edit, List<Given> broken)
    {
        foreach (
            var (bound, limit, outside) in new[]
            {
                ("max", edit.Now.Max, broken.Where(given => FactValues.Parse(given.Value) > edit.Now.Max).ToList()),
                ("min", edit.Now.Min, broken.Where(given => FactValues.Parse(given.Value) < edit.Now.Min).ToList()),
            }
        )
        {
            if (outside.Count > 0)
            {
                var values = string.Join(", ", outside.Select(given => given.Value).Distinct(StringComparer.Ordinal));
                yield return Cause(
                    MistakeCode.FactRangeInUse,
                    edit,
                    bound,
                    string.Create(CultureInfo.InvariantCulture, $"'{edit.Now.Name}' cannot have {bound} {limit}: {values} is used."),
                    "Change these steps first, or keep the range.",
                    outside
                );
            }
        }
    }

    private static void TypeChanged(FactEdit edit, List<Mistake> kept, List<Mistake> causes)
    {
        if (edit.Uses.Count == 0)
        {
            return;
        }
        kept.RemoveAll(mistake =>
            edit.Uses.Any(use =>
                mistake.Resource == Resource(use.Procedure)
                && (mistake.Path == use.Use.Path || mistake.Path.StartsWith($"{use.Use.Path}.", StringComparison.Ordinal))
            )
        );
        causes.Add(
            Cause(
                MistakeCode.FactTypeInUse,
                edit,
                "type",
                $"'{edit.Now.Name}' is used, so its type cannot change from {edit.Was.Type} to {edit.Now.Type}.",
                "Change these steps first, or add a new fact with the new type.",
                edit.Uses.Select(use => new Given(use.Procedure, use.Use, use.Use.Path, null, ""))
            )
        );
    }

    private static void NoLongerWritable(FactEdit edit, List<Mistake> kept, List<Mistake> causes)
    {
        if (edit.Was.WritableBy == FactWriters.Assign || edit.Now.WritableBy != FactWriters.Assign)
        {
            return;
        }
        var collects = edit
            .Uses.Where(use => use.Use.Use == FactUse.Collect)
            .Select(use => new Given(use.Procedure, use.Use, use.Use.Path, MistakeCode.FactCollectNotWritable, ""))
            .Where(given => kept.Any(mistake => IsAt(mistake, given.Code, Resource(given.Procedure), given.Path)))
            .ToList();
        if (collects.Count == 0)
        {
            return;
        }
        kept.RemoveAll(mistake => collects.Any(given => IsAt(mistake, given.Code, Resource(given.Procedure), given.Path)));
        var where = string.Join(
            ", ",
            collects.Select(given => $"'collect' of step '{Step(given.Use)}' in '{given.Procedure.Id}'").Distinct(StringComparer.Ordinal)
        );
        causes.Add(
            Cause(
                MistakeCode.FactWritableByInUse,
                edit,
                "writableBy",
                $"{where} names fact '{edit.Now.Name}', which only an assign step may write.",
                "Remove it from collect, or give the fact 'writableBy: set'.",
                collects,
                listed: false
            )
        );
    }

    private static IEnumerable<Given> Values(FactUseIn use)
    {
        var (procedure, reference) = use;
        var name = reference.To.Id;
        if (reference.Use == FactUse.Assign && procedure.Steps[Step(reference)].Assign is { } assign)
        {
            yield return new Given(procedure, reference, reference.Path, MistakeCode.AssignValueInvalid, assign[name]);
        }
        foreach (var condition in ProcedureFactRules.Conditions(procedure).Where(condition => condition.Path == reference.Path))
        {
            foreach (var (value, index) in (condition.Condition.OneOf ?? []).Select((value, index) => (value, index)))
            {
                yield return new Given(
                    procedure,
                    reference,
                    string.Create(CultureInfo.InvariantCulture, $"{reference.Path}.oneOf[{index}]"),
                    MistakeCode.ConditionValueInvalid,
                    value
                );
            }
            foreach (var bound in new[] { condition.Condition.Range?.Min, condition.Condition.Range?.Max }.OfType<int>())
            {
                yield return new Given(
                    procedure,
                    reference,
                    $"{reference.Path}.range",
                    MistakeCode.ConditionValueInvalid,
                    bound.ToString(CultureInfo.InvariantCulture)
                );
            }
        }
    }

    private static Mistake Cause(
        MistakeCode code,
        FactEdit edit,
        string field,
        string message,
        string fix,
        IEnumerable<Given> givens,
        bool listed = true
    )
    {
        var referrers = givens
            .DistinctBy(given => (given.Procedure.Id, given.Use.Path))
            .OrderBy(given => given.Procedure.Id, StringComparer.Ordinal)
            .ThenBy(given => given.Use.Path, StringComparer.Ordinal)
            .ToList();
        var list = string.Join(", ", referrers.Select(given => $"procedure {given.Procedure.Id}, {Referrer(given.Use)}"));
        return new Mistake(
            code,
            ValidationStage.Save,
            edit.Owner,
            edit.Prefix + field,
            listed ? $"{message} Used by: {list}." : message,
            fix,
            [.. referrers.Select(given => new RelatedResource(Resource(given.Procedure), given.Use.Path))]
        );
    }

    private static ResourceRef Resource(Procedure procedure) => new(ResourceKind.Procedure, procedure.Id);

    private static string Step(Reference use) => use.Path.Split('.')[1];

    private static void RemovedLocal(Procedure procedure, string name, List<Mistake> kept, List<Mistake> causes)
    {
        var resource = new ResourceRef(ResourceKind.Procedure, procedure.Id);
        var uses = ReferenceWalker
            .Uses(procedure)
            .Where(use =>
                use.To == new ResourceRef(ResourceKind.GlobalFact, name)
                && kept.Any(mistake => IsAt(mistake, MistakeCode.ReferenceMissing, resource, use.Path))
            )
            .ToList();
        if (uses.Count == 0)
        {
            return;
        }
        kept.RemoveAll(mistake => uses.Any(use => IsAt(mistake, MistakeCode.ReferenceMissing, resource, use.Path)));
        causes.Add(
            new Mistake(
                MistakeCode.ReferenceMissing,
                ValidationStage.Save,
                resource,
                $"localFacts.{name}",
                $"'{procedure.Id}' no longer declares local fact '{name}', which is used by: {string.Join(", ", uses.Select(Referrer).Distinct(StringComparer.Ordinal))}.",
                "Change these steps first, or keep the fact.",
                [.. uses.Select(use => new RelatedResource(resource, use.Path))]
            )
        );
    }

    private static bool IsAt(Mistake mistake, MistakeCode? code, ResourceRef resource, string path) =>
        mistake.Code == code && mistake.Resource == resource && mistake.Path == path;

    private static string Referrer(Reference use) =>
        $"step {Step(use)} ({use.Use switch
        {
            FactUse.Collect => "collect",
            FactUse.Assign => "assign",
            FactUse.Condition => "condition",
            FactUse.Parameter => "with",
            FactUse.Result => "results",
            FactUse.Say => "say",
            _ => throw new ArgumentOutOfRangeException(nameof(use)),
        }})";
}
