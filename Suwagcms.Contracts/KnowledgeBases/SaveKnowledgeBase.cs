namespace Suwagcms.Contracts.KnowledgeBases;

public sealed record SaveKnowledgeBase(string? Title, string? Language, string? Embedding, int? Results);
