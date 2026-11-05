namespace Suwagcms.Sdk.Format;

public sealed record BotDefinition
{
    public const int CurrentFormatVersion = DefinitionFormat.Version;

    public required int FormatVersion { get; init; }

    public IReadOnlyList<GlobalFact> Facts { get; init; } = [];

    public IReadOnlyList<KnowledgeBase> KnowledgeBases { get; init; } = [];

    public IReadOnlyList<Article> Articles { get; init; } = [];

    public IReadOnlyList<BotAction> Actions { get; init; } = [];

    public IReadOnlyList<Procedure> Procedures { get; init; } = [];

    public Persona? Persona { get; init; }
}
