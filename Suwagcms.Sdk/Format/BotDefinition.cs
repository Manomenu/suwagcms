using System.Text.Json;
using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.Articles;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.KnowledgeBases;
using Suwagcms.Sdk.Format.Personas;
using Suwagcms.Sdk.Format.Procedures;

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

    public static BotDefinition Read(string json) =>
        JsonSerializer.Deserialize<BotDefinition>(json, DefinitionFormat.Options)
        ?? throw new InvalidDataException("This document is null, not a definition.");
}
