using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Validation;

internal static class ReferenceRules
{
    public static IReadOnlyList<Rule> All { get; } =
    [
        new(MistakeCodes.ReferenceMissing, ValidationStage.Save, MissingTargets),
        new(MistakeCodes.ReferenceMalformed, ValidationStage.Save, definition => Malformed(definition).Concat(WrongNames(definition))),
        new(MistakeCodes.ReferenceLoop, ValidationStage.Save, LoopRules.Loops),
    ];

    // Why: facts, articles and procedures share the one code, so a client treats a name that points at nothing the same way wherever it is written.
    private static IEnumerable<Mistake> MissingTargets(BotDefinition definition) =>
        ProcedureFactRules.MissingFacts(definition).Concat(MissingResources(definition));

    private static IEnumerable<Mistake> MissingResources(BotDefinition definition)
    {
        var known = definition
            .Articles.Select(article => new ResourceRef(ResourceKind.Article, article.Id))
            .Concat(definition.Procedures.Select(procedure => new ResourceRef(ResourceKind.Procedure, procedure.Id)))
            .ToHashSet();
        var procedures = definition.Procedures.Select(procedure => procedure.Id).Order(StringComparer.Ordinal).ToList();
        return ReferenceWalker
            .From(definition)
            .Where(reference => reference.To.Kind != ResourceKind.GlobalFact)
            .Where(reference => !WrongName(reference))
            .Where(reference => !known.Contains(reference.To))
            .DistinctBy(reference => (reference.From, reference.Path, reference.To))
            .Select(reference => new Mistake(
                MistakeCodes.ReferenceMissing,
                ValidationStage.Save,
                reference.From,
                reference.Path,
                Missing(reference),
                reference.To.Kind == ResourceKind.Article
                    ? "Pick an existing article; 'suwagcms articles list' shows them."
                    : $"Procedures: {string.Join(", ", procedures)}.",
                []
            ));
    }

    private static string Missing(Reference reference) =>
        reference.From.Kind == ResourceKind.Article
            ? $"{reference.Path} of '{reference.From.Id}' links to article '{reference.To.Id}', which this bot does not have."
            : $"{Holder(reference)} names {DeleteGuard.Noun(reference.To.Kind)} '{reference.To.Id}', which this bot does not have.";

    // Why: the path of a reference in a procedure says which block holds it, so one sentence serves every block.
    private static string Holder(Reference reference)
    {
        var parts = reference.Path.Split('.');
        if (parts[0] == "whenStalled")
        {
            return $"'context' of whenStalled in '{reference.From.Id}'";
        }
        return $"'{(parts.Length > 3 && parts[3] == "context" ? "context" : parts[2])}' of step '{parts[1]}' in '{reference.From.Id}'";
    }

    // Why: a link's id is judged by the link's own form, and a collect or a rule names a fact it then finds missing; a name an author writes for a target has to have the right form before it can be looked up, and saying so beats saying it is missing.
    public static bool WrongName(Reference reference) =>
        reference.From.Kind == ResourceKind.Procedure
        && (
            reference.To.Kind == ResourceKind.GlobalFact
                ? reference.Use == FactUse.Inform && !FactNames.IsValid(reference.To.Id)
                : !ResourceIds.IsCamelCase(reference.To.Id)
        );

    private static IEnumerable<Mistake> WrongNames(BotDefinition definition) =>
        ReferenceWalker
            .From(definition)
            .Where(WrongName)
            .Select(reference => new Mistake(
                MistakeCodes.ReferenceMalformed,
                ValidationStage.Save,
                reference.From,
                reference.Path,
                reference.To.Kind == ResourceKind.GlobalFact
                    ? $"{Holder(reference)} names '{reference.To.Id}', which is not the name of a fact: camelCase words joined by dots, like customer.city."
                    : $"{Holder(reference)} names '{reference.To.Id}', which is not an id: the id of {An(reference.To.Kind)} is one camelCase word.",
                reference.To.Kind == ResourceKind.GlobalFact
                    ? $"Use the name of the fact, like '{FactNames.Suggest(reference.To.Id)}'."
                    : $"Use the id of the {DeleteGuard.Noun(reference.To.Kind)}, like '{ResourceIds.Suggest(reference.To.Id)}'.",
                []
            ));

    private static string An(ResourceKind kind) => kind == ResourceKind.Article ? "an article" : "a procedure";

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
