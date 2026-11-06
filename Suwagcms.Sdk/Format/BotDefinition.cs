using System.Text.Json;

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

    // Why: a bot reads a definition through this rather than a copy of the options, so a newer optional field or a strict number reads alike in the bot and the SDK.
    public static BotDefinition Read(string json) =>
        JsonSerializer.Deserialize<BotDefinition>(json, DefinitionFormat.Options)
        ?? throw new InvalidDataException("This document is null, not a definition.");
}
