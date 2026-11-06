using Suwagcms.Sdk.Format.Procedures.Blocks;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Sdk.Format.Procedures;

// Why: an article is a reference that cannot miss and is checked here; a query is text only the bot's search can answer, so the CMS stores it and never runs it.
public sealed record InstructionContext
{
    public IReadOnlyList<string>? Articles { get; init; }

    public IReadOnlyList<string>? SearchQueries { get; init; }

    public IEnumerable<BlockTarget> TargetsAt(string path) =>
        (Articles ?? []).Select((id, index) => new BlockTarget(new ResourceRef(ResourceKind.Article, id), $"{path}.articles[{index}]"));
}
