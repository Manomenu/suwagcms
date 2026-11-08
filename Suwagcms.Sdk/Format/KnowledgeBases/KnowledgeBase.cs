namespace Suwagcms.Sdk.Format.KnowledgeBases;

public sealed record KnowledgeBase
{
    public const string DefaultEmbedding = "default";

    public const int DefaultResults = 5;

    public const int MinResults = 1;

    public const int MaxResults = 20;

    public required string Id { get; init; }

    public string Title { get; init; } = "";

    public string Language { get; init; } = KnowledgeBaseLanguages.English;

    // Why: only the name of a profile; its model, address and key live in the bot's environment, so a version can be exported and read by anyone.
    public required string Embedding { get; init; }

    public required int Results { get; init; }
}
