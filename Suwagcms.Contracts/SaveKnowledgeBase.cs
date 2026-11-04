namespace Suwagcms.Contracts;

/// <summary>What saving a knowledge base takes: its title; the id comes from the address. A title left out is refused by the server.</summary>
public sealed record SaveKnowledgeBase(string? Title);
