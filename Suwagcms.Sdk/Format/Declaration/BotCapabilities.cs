namespace Suwagcms.Sdk.Format.Declaration;

public sealed record BotCapabilities
{
    public required int FormatVersion { get; init; }

    public required IReadOnlyList<string> Blocks { get; init; }

    public IReadOnlyList<CodeAction> CodeActions { get; init; } = [];

    public string? Persona { get; init; }

    public bool Knowledge { get; init; }

    public IReadOnlyList<string> EmbeddingProfiles { get; init; } = [];

    public int? NamedArticlesLimit { get; init; }
}
