namespace Suwagcms.Contracts.Articles;

public sealed record SaveArticle(string? KnowledgeBase, string? Title, string? Content, string? LlmGuide, string? Description);
