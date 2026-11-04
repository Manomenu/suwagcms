namespace Suwagcms.Contracts;

/// <summary>What saving an article takes: every field of it but the id, which the address gives. A field left out is refused by the server, except language: a new article has en, and an existing one keeps its own; content and llmGuide are Markdown, kept exactly as sent.</summary>
public sealed record SaveArticle(
    string? KnowledgeBase,
    string? Language,
    string? Title,
    string? Content,
    string? LlmGuide,
    string? Description
);
