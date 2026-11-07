namespace Suwagcms.Contracts.KnowledgeBases;

/// <summary>What saving a knowledge base takes: its title, its language, the name of the embedding profile the bot embeds its articles with, and how many results one search gives from it (1 to 20); the id comes from the address. A title, profile or results left out is refused by the server; a language left out is en.</summary>
public sealed record SaveKnowledgeBase(string? Title, string? Language, string? Embedding, int? Results);
