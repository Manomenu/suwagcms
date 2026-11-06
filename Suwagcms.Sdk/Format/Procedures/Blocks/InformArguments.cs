namespace Suwagcms.Sdk.Format.Procedures.Blocks;

public sealed record InformArguments
{
    public string Instruction { get; init; } = "";

    public IReadOnlyList<KnowledgeEntry>? Knowledge { get; init; }

    // Why: name to what must be said, in the order written; the bot checks each name was told, so a point is named and an order is kept.
    public IReadOnlyDictionary<string, string?>? Points { get; init; }
}
