namespace Suwagcms.Contracts;

/// <summary>A knowledge base of a bot as the API shows it: its id and title, and the revision to send back to guard an edit.</summary>
public sealed record KnowledgeBaseView(string Id, string Title, int Revision);
