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
        return [.. kept, .. causes];
    }

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
        $"step {use.Path.Split('.')[1]} ({use.Use switch
        {
            FactUse.Collect => "collect",
            FactUse.Assign => "assign",
            FactUse.Condition => "when",
            _ => throw new ArgumentOutOfRangeException(nameof(use)),
        }})";
}
