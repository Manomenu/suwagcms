using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Validation;

internal static class KnowledgeBaseRules
{
    public static IReadOnlyList<Rule> All { get; } =
    [
        For(MistakeCodes.KnowledgeBaseIdInvalid, definition => definition.KnowledgeBases.SelectMany(Id)),
        For(MistakeCodes.KnowledgeBaseTitleMissing, definition => definition.KnowledgeBases.SelectMany(Title)),
        For(MistakeCodes.KnowledgeBaseTitleRepeated, Repeated),
    ];

    private static Rule For(string code, Func<BotDefinition, IEnumerable<Mistake>> check) => new(code, ValidationStage.Save, check);

    private static IEnumerable<Mistake> Id(KnowledgeBase knowledgeBase)
    {
        if (!ResourceIds.IsCamelCase(knowledgeBase.Id))
        {
            yield return Make(
                MistakeCodes.KnowledgeBaseIdInvalid,
                knowledgeBase,
                "id",
                $"'{knowledgeBase.Id}' is not a knowledge base id: it is camelCase, like offer or forValuers.",
                $"Rename the knowledge base to '{ResourceIds.Suggest(knowledgeBase.Id)}'."
            );
        }
    }

    private static IEnumerable<Mistake> Title(KnowledgeBase knowledgeBase)
    {
        if (string.IsNullOrWhiteSpace(knowledgeBase.Title))
        {
            yield return Make(
                MistakeCodes.KnowledgeBaseTitleMissing,
                knowledgeBase,
                "title",
                $"Knowledge base '{knowledgeBase.Id}' has no title.",
                "Add one: people tell the bases apart by their titles."
            );
        }
    }

    // Why: a saved definition never holds a repeated title, so the base being saved is among those reported and each one names the others.
    private static IEnumerable<Mistake> Repeated(BotDefinition definition) =>
        definition
            .KnowledgeBases.Select((item, index) => (item, index))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.item.Title))
            .GroupBy(entry => entry.item.Title.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .SelectMany(group =>
                group.Select(entry =>
                {
                    var others = group.Where(other => other.index != entry.index).ToList();
                    var names = string.Join(", ", others.Select(other => $"'{other.item.Id}'"));
                    return Make(
                        MistakeCodes.KnowledgeBaseTitleRepeated,
                        entry.item,
                        "title",
                        $"The title '{entry.item.Title.Trim()}' of knowledge base '{entry.item.Id}' is used by knowledge base {names}.",
                        "Choose another title: two bases with one title cannot be told apart.",
                        [.. others.Select(other => new RelatedResource(Resource(other.item), "title"))]
                    );
                })
            );

    private static ResourceRef Resource(KnowledgeBase knowledgeBase) => new(ResourceKind.KnowledgeBase, knowledgeBase.Id);

    private static Mistake Make(
        string code,
        KnowledgeBase knowledgeBase,
        string path,
        string message,
        string fix,
        IReadOnlyList<RelatedResource>? related = null
    ) => new(code, ValidationStage.Save, Resource(knowledgeBase), path, message, fix, related ?? []);
}
