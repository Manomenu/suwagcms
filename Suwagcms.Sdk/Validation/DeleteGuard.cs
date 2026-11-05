using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Validation;

// Why: a delete is guarded by the missing-reference rule on the definition left behind; this only words its findings for the resource at fault, once per removed resource, so a client lists every referrer at once.
public static class DeleteGuard
{
    public static IReadOnlyList<Mistake> Reshape(
        BotDefinition after,
        IReadOnlyCollection<ResourceRef> removed,
        IReadOnlyList<Mistake> mistakes
    )
    {
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(removed);
        ArgumentNullException.ThrowIfNull(mistakes);
        var missing = mistakes.Where(mistake => mistake.Code == MistakeCodes.ReferenceMissing).ToList();
        var blocking = ReferenceWalker
            .From(after)
            .Where(reference => removed.Contains(reference.To))
            .Where(reference => missing.Any(mistake => mistake.Resource == reference.From && mistake.Path == reference.Path))
            .DistinctBy(reference => (reference.From, reference.Path, reference.To))
            .ToList();
        if (blocking.Count == 0)
        {
            return mistakes;
        }
        var explained = mistakes.Where(mistake =>
            !(
                mistake.Code == MistakeCodes.ReferenceMissing
                && blocking.Any(reference => reference.From == mistake.Resource && reference.Path == mistake.Path)
            )
        );
        return [.. explained, .. blocking.GroupBy(reference => reference.To).Select(Refusal)];
    }

    private static Mistake Refusal(IGrouping<ResourceRef, Reference> group)
    {
        var target = group.Key;
        var noun = Noun(target.Kind);
        var referrers = group
            .OrderBy(reference => reference.From.Kind)
            .ThenBy(reference => reference.From.Id, StringComparer.Ordinal)
            .ThenBy(reference => reference.Path, StringComparer.Ordinal)
            .ToList();
        var where = string.Join(
            ", ",
            referrers.Select(reference => $"{reference.Path} of {Noun(reference.From.Kind)} '{reference.From.Id}'")
        );
        return new Mistake(
            MistakeCodes.ReferenceMissing,
            ValidationStage.Save,
            target,
            "name",
            $"The {noun} '{target.Id}' is used in {referrers.Count} place(s), so it cannot be deleted. Used by: {where}.",
            $"Remove the reference to '{target.Id}' in each place listed in `related`, then delete the {noun} again.",
            [.. referrers.Select(reference => new RelatedResource(reference.From, reference.Path))]
        );
    }

    internal static string Noun(ResourceKind kind) =>
        kind switch
        {
            ResourceKind.GlobalFact => "fact",
            ResourceKind.KnowledgeBase => "knowledge base",
            ResourceKind.Article => "article",
            ResourceKind.Procedure => "procedure",
            ResourceKind.Action => "action",
            ResourceKind.Definition => "definition",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
