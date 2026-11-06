namespace Suwagcms.Sdk.Format.Knowledge;

public sealed record KnowledgeBase
{
    public required string Id { get; init; }

    public string Title { get; init; } = "";

    public string Language { get; init; } = KnowledgeBaseLanguages.English;
}
