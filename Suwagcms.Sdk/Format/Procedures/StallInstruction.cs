namespace Suwagcms.Sdk.Format.Procedures;

public sealed record StallInstruction
{
    public string Instruction { get; init; } = "";

    public IReadOnlyList<KnowledgeEntry>? Knowledge { get; init; }
}
