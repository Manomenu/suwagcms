using System.Globalization;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Knowledge;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Sdk.Validation.Rules;

internal static class KnowledgeBaseRules
{
    public static IReadOnlyList<Rule> All { get; } =
    [
        For(MistakeCodes.KnowledgeBaseIdInvalid, definition => definition.KnowledgeBases.SelectMany(Id)),
        For(MistakeCodes.KnowledgeBaseTitleMissing, definition => definition.KnowledgeBases.SelectMany(Title)),
        For(MistakeCodes.KnowledgeBaseTitleRepeated, Repeated),
        For(MistakeCodes.KnowledgeBaseLanguageUnknown, definition => definition.KnowledgeBases.SelectMany(Language)),
        For(MistakeCodes.KnowledgeBaseEmbeddingMissing, definition => definition.KnowledgeBases.SelectMany(Embedding)),
        For(MistakeCodes.KnowledgeBaseResultsOutOfRange, definition => definition.KnowledgeBases.SelectMany(Results)),
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

    // Why: the value must match exactly, in lowercase: accepting PL would need a rule for which spelling gets stored.
    private static IEnumerable<Mistake> Language(KnowledgeBase knowledgeBase)
    {
        if (!KnowledgeBaseLanguages.All.Contains(knowledgeBase.Language, StringComparer.Ordinal))
        {
            yield return Make(
                MistakeCodes.KnowledgeBaseLanguageUnknown,
                knowledgeBase,
                "language",
                string.IsNullOrWhiteSpace(knowledgeBase.Language)
                    ? $"Knowledge base '{knowledgeBase.Id}' has no language."
                    : $"Knowledge base '{knowledgeBase.Id}' has language '{knowledgeBase.Language}'.",
                $"Use one of: {string.Join(", ", KnowledgeBaseLanguages.All)}."
            );
        }
    }

    // Why: whether the bot serves the profile is known only from its declaration, which never stops a save, so any name is kept here and judged when a version is created.
    private static IEnumerable<Mistake> Embedding(KnowledgeBase knowledgeBase)
    {
        if (string.IsNullOrWhiteSpace(knowledgeBase.Embedding))
        {
            yield return Make(
                MistakeCodes.KnowledgeBaseEmbeddingMissing,
                knowledgeBase,
                "embedding",
                $"Knowledge base '{knowledgeBase.Id}' names no embedding profile.",
                $"Type the profile the bot embeds its articles with, such as {KnowledgeBase.DefaultEmbedding}."
            );
        }
    }

    // Why: the bot's environment decides which profiles it serves, so this is the declaration's check at Version, never a rule at Save.
    public static IEnumerable<Mistake> UndeclaredProfiles(BotDefinition definition, IReadOnlyList<string> profiles)
    {
        var served = profiles.Count == 0 ? "none" : string.Join(", ", profiles);
        return definition
            .KnowledgeBases.Where(knowledgeBase => !profiles.Contains(knowledgeBase.Embedding, StringComparer.OrdinalIgnoreCase))
            .OrderBy(knowledgeBase => knowledgeBase.Id, StringComparer.Ordinal)
            .Select(knowledgeBase => new Mistake(
                MistakeCodes.DeclarationEmbeddingProfile,
                ValidationStage.Version,
                Resource(knowledgeBase),
                "embedding",
                $"Knowledge base '{knowledgeBase.Id}' names the embedding profile '{knowledgeBase.Embedding}', which this bot does not serve. It serves: {served}.",
                $"Type one of the profiles the bot serves on the knowledge base, or set the variables of '{knowledgeBase.Embedding}' on the bot and deploy.",
                []
            ));
    }

    private static IEnumerable<Mistake> Results(KnowledgeBase knowledgeBase)
    {
        if (knowledgeBase.Results is < KnowledgeBase.MinResults or > KnowledgeBase.MaxResults)
        {
            yield return Make(
                MistakeCodes.KnowledgeBaseResultsOutOfRange,
                knowledgeBase,
                "results",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Knowledge base '{knowledgeBase.Id}' gives {knowledgeBase.Results} results per search."
                ),
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Set a whole number from {KnowledgeBase.MinResults} to {KnowledgeBase.MaxResults}, such as {KnowledgeBase.DefaultResults}."
                )
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
