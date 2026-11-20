using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Articles;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation.Guards;

namespace Suwagcms.Sdk.Validation.Rules;

internal static class ReferenceRules
{
    public static IReadOnlyList<Rule> All { get; } =
    [
        new(MistakeCodes.ReferenceMissing, ValidationStage.Save, MissingTargets),
        new(MistakeCodes.ReferenceMalformed, ValidationStage.Save, definition => Malformed(definition).Concat(WrongNames(definition))),
        new(MistakeCodes.ReferenceLoop, ValidationStage.Save, LoopRules.Loops),
        new(MistakeCodes.LinkProcedureNotInGuide, ValidationStage.Save, ProcedureLinksInContent),
        new(MistakeCodes.LinkProcedureConversation, ValidationStage.Save, LinksToConversation),
    ];

    private static IEnumerable<Mistake> MissingTargets(BotDefinition definition) =>
        ProcedureFactRules
            .MissingFacts(definition)
            .Concat(MissingResources(definition))
            .Concat(MissingActions(definition))
            .Concat(ActionBlockRules.MissingMappedFields(definition));

    private static IEnumerable<Mistake> MissingActions(BotDefinition definition)
    {
        var known = definition.Actions.Select(action => action.Id).Order(StringComparer.Ordinal).ToList();
        return ReferenceWalker
            .From(definition)
            .Where(reference => reference.To.Kind == ResourceKind.Action && !known.Contains(reference.To.Id, StringComparer.Ordinal))
            .Select(reference => new Mistake(
                MistakeCodes.ReferenceMissing,
                ValidationStage.Save,
                reference.From,
                reference.Path,
                $"{reference.Path} of '{reference.From.Id}' names action '{reference.To.Id}', which this bot does not have.",
                known.Count == 0 ? "The bot has no actions yet: add one first." : $"Actions of the bot: {string.Join(", ", known)}.",
                []
            ));
    }

    private static IEnumerable<Mistake> MissingResources(BotDefinition definition)
    {
        var known = definition
            .Articles.Select(article => new ResourceRef(ResourceKind.Article, article.Id))
            .Concat(definition.Procedures.Select(procedure => new ResourceRef(ResourceKind.Procedure, procedure.Id)))
            .Concat(definition.KnowledgeBases.Select(knowledgeBase => new ResourceRef(ResourceKind.KnowledgeBase, knowledgeBase.Id)))
            .ToHashSet();
        var bases = definition.KnowledgeBases.Select(knowledgeBase => knowledgeBase.Id).Order(StringComparer.Ordinal).ToList();
        var procedures = definition.Procedures.Select(procedure => procedure.Id).Order(StringComparer.Ordinal).ToList();
        return ReferenceWalker
            .From(definition)
            .Where(reference =>
                reference.To.Kind is ResourceKind.Article or ResourceKind.Procedure
                || (reference.To.Kind == ResourceKind.KnowledgeBase && reference.From.Kind == ResourceKind.Procedure)
            )
            .Where(reference => !WrongName(reference))
            .Where(reference => !known.Contains(reference.To))
            .DistinctBy(reference => (reference.From, reference.Path, reference.To))
            .Select(reference => new Mistake(
                MistakeCodes.ReferenceMissing,
                ValidationStage.Save,
                reference.From,
                reference.Path,
                Missing(reference),
                reference.To.Kind == ResourceKind.KnowledgeBase
                        ? (
                            bases.Count == 0
                                ? "This bot has no knowledge bases yet: add one first."
                                : $"Knowledge bases of the bot: {string.Join(", ", bases)}."
                        )
                    : reference.To.Kind == ResourceKind.Article ? "Pick an existing article; 'suwagcms articles list' shows them."
                    : reference.From.Kind == ResourceKind.Article ? "Pick an existing procedure; 'suwagcms procedures list' shows them."
                    : $"Procedures: {string.Join(", ", procedures)}.",
                []
            ));
    }

    private static string Missing(Reference reference) =>
        reference.From.Kind == ResourceKind.Article
            ? $"{reference.Path} of '{reference.From.Id}' links to {DeleteGuard.Noun(reference.To.Kind)} '{reference.To.Id}', which this bot does not have."
            : $"{Holder(reference)} names {DeleteGuard.Noun(reference.To.Kind)} '{reference.To.Id}', which this bot does not have.";

    private static string Holder(Reference reference)
    {
        var parts = reference.Path.Split('.');
        return $"'{(parts.Length > 3 && parts[3].StartsWith("knowledge", StringComparison.Ordinal) ? "knowledge" : parts[2])}' of step '{parts[1]}' in '{reference.From.Id}'";
    }

    public static bool WrongName(Reference reference) =>
        reference.From.Kind == ResourceKind.Procedure
        && reference.To.Kind is ResourceKind.Article or ResourceKind.Procedure
        && !ResourceIds.IsCamelCase(reference.To.Id);

    private static IEnumerable<Mistake> WrongNames(BotDefinition definition) =>
        ReferenceWalker
            .From(definition)
            .Where(WrongName)
            .Select(reference => new Mistake(
                MistakeCodes.ReferenceMalformed,
                ValidationStage.Save,
                reference.From,
                reference.Path,
                $"{Holder(reference)} names '{reference.To.Id}', which is not an id: the id of {An(reference.To.Kind)} is one camelCase word.",
                $"Use the id of the {DeleteGuard.Noun(reference.To.Kind)}, like '{ResourceIds.Suggest(reference.To.Id)}'.",
                []
            ));

    private static string An(ResourceKind kind) => kind == ResourceKind.Article ? "an article" : "a procedure";

    private static IEnumerable<Mistake> Malformed(BotDefinition definition) =>
        definition.Articles.SelectMany(article =>
            new[] { ("content", article.Content), ("llmGuide", article.LlmGuide) }
                .SelectMany(field =>
                    CmsLinks.In(field.Item2).Where(link => link.Form == CmsLinkForm.Malformed).Select(link => (field.Item1, link))
                )
                .DistinctBy(entry => (entry.Item1, entry.link.Target))
                .Select(entry =>
                    Make(
                        MistakeCodes.ReferenceMalformed,
                        article.Id,
                        entry.Item1,
                        $"{entry.Item1} of '{article.Id}' has the link target '{entry.link.Target}', which is not a link to an article or a procedure.",
                        "Link an article as [Title](cms://article/<id>), with the id of an existing article; in llmGuide, link a procedure as [label](cms://procedure/<id>)."
                    )
                )
        );

    private static IEnumerable<Mistake> ProcedureLinksInContent(BotDefinition definition) =>
        definition.Articles.SelectMany(article =>
            CmsLinks
                .In(article.Content)
                .Where(link => link.Form == CmsLinkForm.Procedure)
                .DistinctBy(link => link.Id)
                .Select(link =>
                    Make(
                        MistakeCodes.LinkProcedureNotInGuide,
                        article.Id,
                        "content",
                        $"content of '{article.Id}' links to procedure '{link.Id}'. A customer reads content, so it cannot start anything.",
                        "Move the link to llmGuide."
                    )
                )
        );

    private static IEnumerable<Mistake> LinksToConversation(BotDefinition definition) =>
        definition
            .Articles.Where(article =>
                CmsLinks.In(article.LlmGuide).Any(link => link.Form == CmsLinkForm.Procedure && link.Id == Procedure.ConversationId)
            )
            .Select(article =>
                Make(
                    MistakeCodes.LinkProcedureConversation,
                    article.Id,
                    "llmGuide",
                    $"llmGuide of '{article.Id}' links to '{Procedure.ConversationId}', which cannot be started: it is the base of every conversation.",
                    "Remove the link, or link the procedure to start."
                )
            );

    private static Mistake Make(string code, string articleId, string path, string message, string fix) =>
        new(code, ValidationStage.Save, new ResourceRef(ResourceKind.Article, articleId), path, message, fix, []);
}
