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
    }

    // Why: a local fact is contained in its procedure, so a use of one is no reference to another resource and is invisible outside.
    public static IEnumerable<Reference> From(Procedure procedure)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        return Uses(procedure)
            .Where(reference => reference.To.Kind != ResourceKind.GlobalFact || !procedure.Locals.ContainsKey(reference.To.Id));
    }

    // Why: every fact a step names, local or global, for the rules that check a use against the fact it means, and the action it runs, which has no use of a fact to say.
    public static IEnumerable<Reference> Uses(Procedure procedure)
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

    // Why: a rename rewrites exactly what the walker finds, so every edge is known to both in this one file; an edge left out here stays pointing at the old id and is refused as a missing reference.
    public static Article Rewrite(Article article, ResourceRef target, string newId)
    {
        ArgumentNullException.ThrowIfNull(article);
        ArgumentNullException.ThrowIfNull(target);
        if (target.Kind == ResourceKind.KnowledgeBase && article.KnowledgeBase == target.Id)
        {
            return article with { KnowledgeBase = newId };
        }
        return target.Kind != ResourceKind.Article
            ? article
            : article with
            {
                Content = CmsLinks.Rewrite(article.Content, target.Id, newId),
                LlmGuide = CmsLinks.Rewrite(article.LlmGuide, target.Id, newId),
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
            return RewriteArticle(procedure, target.Id, newId);
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
                        Assign = step.Value.Assign?.ToDictionary(entry => Renamed(entry.Key), entry => entry.Value),
                        With = step.Value.With?.ToDictionary(entry => entry.Key, entry => Renamed(entry.Value)),
                        Results = step.Value.Results?.ToDictionary(entry => entry.Key, entry => Renamed(entry.Value)),
                        Inform = step.Value.Inform is { } inform ? inform with { Facts = inform.Facts?.Select(Renamed).ToList() } : null,
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

    // Why: a search query is text that names nothing, so only the articles of a context are rewritten.
    private static Procedure RewriteArticle(Procedure procedure, string oldId, string newId)
    {
        IReadOnlyList<string>? Renamed(IReadOnlyList<string>? ids)
        {
            return ids?.Select(id => id == oldId ? newId : id).ToList();
        }
        InstructionContext? InContext(InstructionContext? context)
        {
            return context is null ? null : context with { Articles = Renamed(context.Articles) };
        }
        return procedure with
        {
            WhenStalled = procedure.WhenStalled is { } stalled ? stalled with { Context = InContext(stalled.Context) } : null,
            Steps = procedure.Steps.ToDictionary(
                step => step.Key,
                step =>
                    step.Value.Inform is { } inform
                        ? step.Value with
                        {
                            Inform = inform with { Articles = Renamed(inform.Articles), Context = InContext(inform.Context) },
                        }
                        : step.Value
            ),
        };
    }

    private static ResourceRef Fact(string name) => new(ResourceKind.GlobalFact, name);
}
