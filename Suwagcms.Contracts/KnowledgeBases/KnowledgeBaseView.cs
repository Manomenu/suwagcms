namespace Suwagcms.Contracts.KnowledgeBases;

public sealed record KnowledgeBaseView(string Id, string Title, string Language, string Embedding, int Results, int Revision);
