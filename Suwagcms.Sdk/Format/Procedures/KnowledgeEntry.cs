using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Sdk.Format.Procedures;

// Why: a bot has several knowledge bases, so what the model should read names its base; an article is a reference that cannot miss, and a query is text only the bot's search can answer, so the CMS stores it and never runs it.
public sealed record KnowledgeEntry
{
    public string KnowledgeBase { get; init; } = "";

    public IReadOnlyList<string>? Articles { get; init; }

    public IReadOnlyList<string>? SearchQueries { get; init; }

    public static IEnumerable<BlockTarget> TargetsAt(string path, IReadOnlyList<KnowledgeEntry>? entries) =>
        (entries ?? []).SelectMany((entry, index) => entry.Targets($"{path}[{index}]"));

    private IEnumerable<BlockTarget> Targets(string path)
    {
        if (!string.IsNullOrWhiteSpace(KnowledgeBase))
        {
            yield return new BlockTarget(new ResourceRef(ResourceKind.KnowledgeBase, KnowledgeBase), $"{path}.knowledgeBase");
        }
        foreach (var (id, index) in (Articles ?? []).Select((id, index) => (id, index)))
        {
            yield return new BlockTarget(new ResourceRef(ResourceKind.Article, id), $"{path}.articles[{index}]");
        }
    }
}
