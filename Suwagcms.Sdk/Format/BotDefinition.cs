namespace Suwagcms.Sdk.Format;

public sealed record BotDefinition
{
    public required int FormatVersion { get; init; }

    public IReadOnlyList<GlobalFact> Facts { get; init; } = [];

    public IReadOnlyList<KnowledgeBase> KnowledgeBases { get; init; } = [];

    public IReadOnlyList<Article> Articles { get; init; } = [];

    public IReadOnlyList<BotAction> Actions { get; init; } = [];

    public IReadOnlyList<Procedure> Procedures { get; init; } = [];
}
