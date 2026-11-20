using Suwagcms.Contracts.Usage;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Server.Editing;

internal static class ResourceUsage
{
    public static UsageView Of(IEnumerable<StoredResource> stored, ResourceRef target)
    {
        var references = ReferenceWalker
            .From(ResourceKinds.Assemble(stored))
            .Where(reference => reference.To.Kind != ResourceKind.KnowledgeBase || reference.From.Kind != ResourceKind.Article)
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
