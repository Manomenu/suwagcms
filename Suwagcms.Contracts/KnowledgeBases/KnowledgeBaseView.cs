namespace Suwagcms.Contracts.KnowledgeBases;

/// <summary>A knowledge base of a bot as the API shows it: its id, title, language, embedding profile and results per search, and the revision to send back to guard an edit.</summary>
public sealed record KnowledgeBaseView(string Id, string Title, string Language, string Embedding, int Results, int Revision);
