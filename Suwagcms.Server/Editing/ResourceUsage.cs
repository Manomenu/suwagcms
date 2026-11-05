using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Server.Editing;

// Why: the lists are computed from the definition by the walker, the same one the delete guard uses, so the panels, a refusal and the question before turning off cannot disagree.
internal static class ResourceUsage
{
    public static UsageView Of(IEnumerable<StoredResource> stored, ResourceRef target)
    {
        // Why: a base lists its own articles on its screen, so that edge is no entry in these panels.
        var references = ReferenceWalker
            .From(ResourceKinds.Assemble(stored))
            .Where(reference => reference.To.Kind != ResourceKind.KnowledgeBase)
            .ToList();
        return new UsageView(
            Links(references.Where(reference => reference.To == target), reference => reference.From),
            Links(references.Where(reference => reference.From == target), reference => reference.To)
        );
    }

    private static List<UsageLink> Links(IEnumerable<Reference> references, Func<Reference, ResourceRef> other) =>
        [
            .. references
                .Select(reference => new UsageLink(ResourceKinds.Noun(other(reference).Kind), other(reference).Id, reference.Path))
                .Distinct()
                .OrderBy(link => link.Kind, StringComparer.Ordinal)
                .ThenBy(link => link.Id, StringComparer.Ordinal)
                .ThenBy(link => link.Field, StringComparer.Ordinal),
        ];
}
