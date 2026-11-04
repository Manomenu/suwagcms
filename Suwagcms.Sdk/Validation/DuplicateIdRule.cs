using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Validation;

internal static class DuplicateIdRule
{
    public static Rule Rule { get; } = new(MistakeCodes.DuplicateId, ValidationStage.Save, Check);

    private static IEnumerable<Mistake> Check(BotDefinition definition) =>
        Duplicates(ResourceKind.GlobalFact, "facts", definition.Facts.Select(fact => fact.Name))
            .Concat(Duplicates(ResourceKind.KnowledgeBase, "knowledgeBases", definition.KnowledgeBases.Select(item => item.Id)))
            .Concat(Duplicates(ResourceKind.Article, "articles", definition.Articles.Select(item => item.Id)))
            .Concat(Duplicates(ResourceKind.Action, "actions", definition.Actions.Select(item => item.Id)))
            .Concat(Duplicates(ResourceKind.Procedure, "procedures", definition.Procedures.Select(item => item.Id)));

    private static IEnumerable<Mistake> Duplicates(ResourceKind kind, string list, IEnumerable<string> ids) =>
        ids.Select((id, index) => (id, index))
            .GroupBy(entry => entry.id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group =>
            {
                var resource = new ResourceRef(kind, group.Key);
                return new Mistake(
                    MistakeCodes.DuplicateId,
                    ValidationStage.Save,
                    resource,
                    kind == ResourceKind.GlobalFact ? "name" : "id",
                    $"The id '{group.Key}' is used by {group.Count()} resources of kind {kind}.",
                    $"Give each {kind} its own id: rename or delete all but one of '{group.Key}'.",
                    [.. group.Select(entry => new RelatedResource(resource, $"{list}[{entry.index}]"))]
                );
            });
}
