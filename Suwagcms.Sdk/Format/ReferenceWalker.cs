namespace Suwagcms.Sdk.Format;

// Why: the one place that knows where a definition names another resource, so a rule, a delete guard and a rename never each learn it separately.
public static class ReferenceWalker
{
    public static IEnumerable<Reference> From(BotDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Procedures.SelectMany(From).Concat(definition.Articles.SelectMany(From));
    }

    public static IEnumerable<Reference> From(Article article)
    {
        ArgumentNullException.ThrowIfNull(article);
        var from = new ResourceRef(ResourceKind.Article, article.Id);
        foreach (var (path, text) in new[] { ("content", article.Content), ("llmGuide", article.LlmGuide) })
        {
            foreach (var link in CmsLinks.In(text).Where(link => link.Form == CmsLinkForm.Article))
            {
                yield return new Reference(from, path, new ResourceRef(ResourceKind.Article, link.Id), null);
            }
        }
    }

    public static IEnumerable<Reference> From(Procedure procedure)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        var from = new ResourceRef(ResourceKind.Procedure, procedure.Id);
        foreach (var target in procedure.WhenStalled?.Context?.TargetsAt("whenStalled.context") ?? [])
        {
            yield return new Reference(from, target.Path, target.To, null);
        }
        foreach (var (stepName, step) in procedure.Steps)
        {
            foreach (var fact in step.Blocks.SelectMany(block => block.Facts))
            {
                yield return new Reference(from, $"steps.{stepName}.{fact.Path}", Fact(fact.Name), fact.Use);
            }
            foreach (var target in step.Blocks.SelectMany(block => block.Targets))
            {
                yield return new Reference(from, $"steps.{stepName}.{target.Path}", target.To, null);
            }
            var rules = step.Next?.Rules ?? [];
            for (var index = 0; index < rules.Count; index++)
            {
                foreach (var name in (rules[index].When ?? new Dictionary<string, Condition>()).Keys)
                {
                    yield return new Reference(from, $"steps.{stepName}.next[{index}].when.{name}", Fact(name), FactUse.Condition);
                }
            }
        }
    }

    private static ResourceRef Fact(string name) => new(ResourceKind.GlobalFact, name);
}
