namespace Suwagcms.Contracts;

/// <summary>What saving an article takes: every field of it but the id, which the address gives. A field left out is refused by the server; an article has no language of its own, so one sent is refused as an unknown field; content and llmGuide are Markdown, kept exactly as sent.</summary>
public sealed record SaveArticle(string? KnowledgeBase, string? Title, string? Content, string? LlmGuide, string? Description);
