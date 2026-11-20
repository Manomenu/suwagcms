namespace Suwagcms.Sdk.Format.Procedures.Blocks;

public sealed record InformArguments
{
    public string Instruction { get; init; } = "";

    public IReadOnlyList<KnowledgeEntry>? Knowledge { get; init; }

    public IReadOnlyDictionary<string, string?>? Points { get; init; }
}
