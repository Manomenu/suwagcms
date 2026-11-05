using System.Globalization;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Validation;

// Why: the rules judge the result of an edit and cannot tell what caused a break; this compares it with what was there and words the refusal for the edit that caused it, once per cause, so an author reads a cause with its list.
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
                    ReferenceWalker.From(procedure).Where(use => use.To.Id == fact.Name).Select(use => new FactUseIn(procedure, use))
                );
                Edited(new Edit(was, fact, new ResourceRef(ResourceKind.GlobalFact, fact.Name), "", [.. uses]), kept, causes);
            }
        }
        foreach (var procedure in after.Procedures)
        {
            var known = before.Procedures.FirstOrDefault(other => other.Id == procedure.Id);
            foreach (var (name, local) in procedure.Locals)
            {
                if (known is not null && known.Locals.TryGetValue(name, out var was) && Differs(was.Named(name), local.Named(name)))
                {
                    var uses = ReferenceWalker.Uses(procedure).Where(use => use.To.Id == name).Select(use => new FactUseIn(procedure, use));
                    Edited(
                        new Edit(
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
        return [.. kept, .. causes];
    }

    private sealed record FactUseIn(Procedure Procedure, Reference Use);

    private sealed record Edit(GlobalFact Was, GlobalFact Now, ResourceRef Owner, string Prefix, IReadOnlyList<FactUseIn> Uses);

    // Why: a value or a bound as a step holds it, with the mistake a rule gives for it on the new definition.
    private sealed record Given(Procedure Procedure, Reference Use, string Path, string Code, string Value);

    private static bool Differs(GlobalFact was, GlobalFact now) =>
        was.Type != now.Type
        || was.WritableBy != now.WritableBy
        || was.Min != now.Min
        || was.Max != now.Max
        || was.MaxLength != now.MaxLength
        || !(was.Values ?? []).SequenceEqual(now.Values ?? [], StringComparer.Ordinal);

    private static void Edited(Edit edit, List<Mistake> kept, List<Mistake> causes)
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
                            MistakeCodes.FactEnumValueInUse,
                            edit,
                            "values",
                            $"Value '{group.Key}' of '{edit.Now.Name}' is used, so it cannot be removed.",
                            "Change these steps first, or keep the value.",
                            group
                        )
                    ),
                FactTypes.WholeNumber => Narrowed(edit, broken),
                FactTypes.Text when broken.Count > 0 =>
                [
                    Cause(
                        MistakeCodes.FactRangeInUse,
                        edit,
                        "maxLength",
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"'{edit.Now.Name}' cannot have maxLength {edit.Now.MaxLength}: a longer value is used."
                        ),
                        "Change these steps first, or keep the length.",
                        broken
                    ),
                ],
                _ => [],
            }
        );
    }

    private static IEnumerable<Mistake> Narrowed(Edit edit, List<Given> broken)
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
                    MistakeCodes.FactRangeInUse,
                    edit,
                    bound,
                    string.Create(CultureInfo.InvariantCulture, $"'{edit.Now.Name}' cannot have {bound} {limit}: {values} is used."),
                    "Change these steps first, or keep the range.",
                    outside
                );
            }
        }
    }

    private static void TypeChanged(Edit edit, List<Mistake> kept, List<Mistake> causes)
    {
        if (edit.Uses.Count == 0)
        {
            return;
        }
        // Why: what no longer fits the new type has one cause, the change of type, so it is said once.
        kept.RemoveAll(mistake =>
            edit.Uses.Any(use =>
                mistake.Resource == Resource(use.Procedure)
                && (mistake.Path == use.Use.Path || mistake.Path.StartsWith($"{use.Use.Path}.", StringComparison.Ordinal))
            )
        );
        causes.Add(
            Cause(
                MistakeCodes.FactTypeInUse,
                edit,
                "type",
                $"'{edit.Now.Name}' is used, so its type cannot change from {edit.Was.Type} to {edit.Now.Type}.",
                "Change these steps first, or add a new fact with the new type.",
                edit.Uses.Select(use => new Given(use.Procedure, use.Use, use.Use.Path, "", ""))
            )
        );
    }

    private static void NoLongerWritable(Edit edit, List<Mistake> kept, List<Mistake> causes)
    {
        if (edit.Was.WritableBy == FactWriters.Assign || edit.Now.WritableBy != FactWriters.Assign)
        {
            return;
        }
        var collects = edit
            .Uses.Where(use => use.Use.Use == FactUse.Collect)
            .Select(use => new Given(use.Procedure, use.Use, use.Use.Path, MistakeCodes.FactCollectNotWritable, ""))
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
                MistakeCodes.FactWritableByInUse,
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
            yield return new Given(procedure, reference, reference.Path, MistakeCodes.AssignValueInvalid, assign[name]);
        }
        foreach (var condition in ProcedureFactRules.Conditions(procedure).Where(condition => condition.Path == reference.Path))
        {
            foreach (var (value, index) in (condition.Condition.OneOf ?? []).Select((value, index) => (value, index)))
            {
                yield return new Given(
                    procedure,
                    reference,
                    string.Create(CultureInfo.InvariantCulture, $"{reference.Path}.oneOf[{index}]"),
                    MistakeCodes.ConditionValueInvalid,
                    value
                );
            }
            foreach (var bound in new[] { condition.Condition.Range?.Min, condition.Condition.Range?.Max }.OfType<int>())
            {
                yield return new Given(
                    procedure,
                    reference,
                    $"{reference.Path}.range",
                    MistakeCodes.ConditionValueInvalid,
                    bound.ToString(CultureInfo.InvariantCulture)
                );
            }
        }
    }

    private static Mistake Cause(
        string code,
        Edit edit,
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
            .Where(use => use.To.Id == name && kept.Any(mistake => IsAt(mistake, MistakeCodes.ReferenceMissing, resource, use.Path)))
            .ToList();
        if (uses.Count == 0)
        {
            return;
        }
        kept.RemoveAll(mistake => uses.Any(use => IsAt(mistake, MistakeCodes.ReferenceMissing, resource, use.Path)));
        causes.Add(
            new Mistake(
                MistakeCodes.ReferenceMissing,
                ValidationStage.Save,
                resource,
                $"localFacts.{name}",
                $"'{procedure.Id}' no longer declares local fact '{name}', which is used by: {string.Join(", ", uses.Select(Referrer).Distinct(StringComparer.Ordinal))}.",
                "Change these steps first, or keep the fact.",
                [.. uses.Select(use => new RelatedResource(resource, use.Path))]
            )
        );
    }

    private static bool IsAt(Mistake mistake, string code, ResourceRef resource, string path) =>
        mistake.Code == code && mistake.Resource == resource && mistake.Path == path;

    private static string Referrer(Reference use) =>
        $"step {Step(use)} ({use.Use switch
        {
            FactUse.Collect => "collect",
            FactUse.Assign => "assign",
            FactUse.Condition => "when",
            _ => throw new ArgumentOutOfRangeException(nameof(use)),
        }})";
}
