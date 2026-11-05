using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Validation;

internal static class ReferenceRules
{
    public static IReadOnlyList<Rule> All { get; } =
    [
        new(MistakeCodes.ReferenceMissing, ValidationStage.Save, MissingTargets),
        new(MistakeCodes.ReferenceMalformed, ValidationStage.Save, Malformed),
        new(MistakeCodes.ReferenceLoop, ValidationStage.Save, LoopRules.Loops),
    ];

    // Why: facts and articles share the one code, so a client treats a name that points at nothing the same way wherever it is written.
    private static IEnumerable<Mistake> MissingTargets(BotDefinition definition) =>
        ProcedureFactRules
            .MissingFacts(definition)
            .Concat(MissingArticles(definition))
            .Concat(MissingActions(definition))
            .Concat(ActionStepRules.MissingMappedFields(definition));

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

    private static IEnumerable<Mistake> MissingArticles(BotDefinition definition)
    {
        var known = definition.Articles.Select(article => article.Id).ToHashSet(StringComparer.Ordinal);
        return ReferenceWalker
            .From(definition)
            .Where(reference => reference.To.Kind == ResourceKind.Article)
            .Where(reference => !known.Contains(reference.To.Id))
            .DistinctBy(reference => (reference.From.Id, reference.Path, reference.To.Id))
            .Select(reference =>
                Make(
                    MistakeCodes.ReferenceMissing,
                    reference.From.Id,
                    reference.Path,
                    $"{reference.Path} of '{reference.From.Id}' links to article '{reference.To.Id}', which this bot does not have.",
                    "Pick an existing article; 'suwagcms articles list' shows them."
                )
            );
    }

    private static IEnumerable<Mistake> Malformed(BotDefinition definition) =>
        definition.Articles.SelectMany(article =>
            new[] { ("content", article.Content), ("llmGuide", article.LlmGuide) }
                .SelectMany(field =>
                    CmsLinks.In(field.Item2).Where(link => link.Form != CmsLinkForm.Article).Select(link => (field.Item1, link))
                )
                .DistinctBy(entry => (entry.Item1, entry.link.Target))
                .Select(entry =>
                    Make(
                        MistakeCodes.ReferenceMalformed,
                        article.Id,
                        entry.Item1,
                        entry.link.Form == CmsLinkForm.Procedure
                            ? $"{entry.Item1} of '{article.Id}' links to a procedure ('{entry.link.Target}'), which an article cannot link to yet."
                            : $"{entry.Item1} of '{article.Id}' has the link target '{entry.link.Target}', which is not an article link.",
                        "Link an article as [Title](cms://article/<id>), with the id of an existing article."
                    )
                )
        );

    private static Mistake Make(string code, string articleId, string path, string message, string fix) =>
        new(code, ValidationStage.Save, new ResourceRef(ResourceKind.Article, articleId), path, message, fix, []);
}
