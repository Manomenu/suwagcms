namespace Suwagcms.Sdk.Format;

public sealed record Article
{
    public const int MaxTextLength = 200_000;

    public required string Id { get; init; }

    public string KnowledgeBase { get; init; } = "";

    public string Title { get; init; } = "";

    public string Content { get; init; } = "";

    public string LlmGuide { get; init; } = "";

    public string Description { get; init; } = "";
}
