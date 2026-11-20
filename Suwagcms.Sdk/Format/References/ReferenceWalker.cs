using Suwagcms.Sdk.Format.Articles;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Procedures;

namespace Suwagcms.Sdk.Format.References;

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
        if (!string.IsNullOrWhiteSpace(article.KnowledgeBase))
        {
            yield return new Reference(from, "knowledgeBase", new ResourceRef(ResourceKind.KnowledgeBase, article.KnowledgeBase), null);
        }
        foreach (var (path, text) in new[] { ("content", article.Content), ("llmGuide", article.LlmGuide) })
        {
            foreach (var link in CmsLinks.In(text).Where(link => link.Form == CmsLinkForm.Article))
            {
                yield return new Reference(from, path, new ResourceRef(ResourceKind.Article, link.Id), null);
            }
        }
        foreach (
            var link in CmsLinks
                .In(article.LlmGuide)
                .Where(link => link.Form == CmsLinkForm.Procedure && link.Id != Procedure.ConversationId)
        )
        {
            yield return new Reference(from, "llmGuide", new ResourceRef(ResourceKind.Procedure, link.Id), null);
        }
    }

    public static IEnumerable<Reference> From(Procedure procedure)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        return Uses(procedure)
            .Where(reference => reference.To.Kind != ResourceKind.GlobalFact || !procedure.Locals.ContainsKey(reference.To.Id));
    }

    public static IEnumerable<Reference> Uses(Procedure procedure)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        var from = new ResourceRef(ResourceKind.Procedure, procedure.Id);
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

    public static Article Rewrite(Article article, ResourceRef target, string newId)
    {
        ArgumentNullException.ThrowIfNull(article);
        ArgumentNullException.ThrowIfNull(target);
        if (target.Kind == ResourceKind.KnowledgeBase && article.KnowledgeBase == target.Id)
        {
            return article with { KnowledgeBase = newId };
        }
        if (target.Kind == ResourceKind.Procedure)
        {
            return article with { LlmGuide = CmsLinks.Rewrite(article.LlmGuide, CmsLinks.ProcedurePrefix, target.Id, newId) };
        }
        return target.Kind != ResourceKind.Article
            ? article
            : article with
            {
                Content = CmsLinks.Rewrite(article.Content, CmsLinks.ArticlePrefix, target.Id, newId),
                LlmGuide = CmsLinks.Rewrite(article.LlmGuide, CmsLinks.ArticlePrefix, target.Id, newId),
            };
    }

    public static Procedure Rewrite(Procedure procedure, ResourceRef target, string newId)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        ArgumentNullException.ThrowIfNull(target);
        if (target.Kind == ResourceKind.Action)
        {
            return procedure with
            {
                Steps = procedure.Steps.ToDictionary(
                    step => step.Key,
                    step => step.Value.Action == target.Id ? step.Value with { Action = newId } : step.Value
                ),
            };
        }
        if (target.Kind == ResourceKind.Procedure)
        {
            return procedure with
            {
                Steps = procedure.Steps.ToDictionary(
                    step => step.Key,
                    step => step.Value.Start == target.Id ? step.Value with { Start = newId } : step.Value
                ),
            };
        }
        if (target.Kind == ResourceKind.Article)
        {
            return RewriteKnowledge(
                procedure,
                entry => entry with { Articles = entry.Articles?.Select(id => id == target.Id ? newId : id).ToList() }
            );
        }
        if (target.Kind == ResourceKind.KnowledgeBase)
        {
            return RewriteKnowledge(procedure, entry => entry.KnowledgeBase == target.Id ? entry with { KnowledgeBase = newId } : entry);
        }
        if (target.Kind != ResourceKind.GlobalFact || procedure.Locals.ContainsKey(target.Id))
        {
            return procedure;
        }
        string Renamed(string name)
        {
            return name == target.Id ? newId : name;
        }
        return procedure with
        {
            Steps = procedure.Steps.ToDictionary(
                step => step.Key,
                step =>
                    step.Value with
                    {
                        Collect = step.Value.Collect?.Select(Renamed).ToList(),
                        Say = step.Value.Say is { } say ? SayPlaceholders.Renamed(say, target.Id, newId) : null,
                        Assign = step.Value.Assign?.ToDictionary(entry => Renamed(entry.Key), entry => entry.Value),
                        With = step.Value.With?.ToDictionary(entry => entry.Key, entry => Renamed(entry.Value)),
                        Results = step.Value.Results?.ToDictionary(entry => entry.Key, entry => Renamed(entry.Value)),
                        Next = step.Value.Next is { Rules: { } rules } next
                            ? next with
                            {
                                Rules =
                                [
                                    .. rules.Select(rule =>
                                        rule with
                                        {
                                            When = rule.When?.ToDictionary(entry => Renamed(entry.Key), entry => entry.Value),
                                        }
                                    ),
                                ],
                            }
                            : step.Value.Next,
                    }
            ),
        };
    }

    private static Procedure RewriteKnowledge(Procedure procedure, Func<KnowledgeEntry, KnowledgeEntry> rewrite) =>
        procedure with
        {
            Steps = procedure.Steps.ToDictionary(
                step => step.Key,
                step =>
                    step.Value.Inform is { } inform
                        ? step.Value with
                        {
                            Inform = inform with { Knowledge = inform.Knowledge?.Select(rewrite).ToList() },
                        }
                        : step.Value
            ),
        };

    private static ResourceRef Fact(string name) => new(ResourceKind.GlobalFact, name);
}
