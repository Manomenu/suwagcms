namespace Suwagcms.Contracts.KnowledgeBases;

/// <summary>What saving a knowledge base takes: its title and its language; the id comes from the address. A title left out is refused by the server; a language left out is en.</summary>
public sealed record SaveKnowledgeBase(string? Title, string? Language);
