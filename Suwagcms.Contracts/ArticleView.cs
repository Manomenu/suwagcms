namespace Suwagcms.Contracts;

/// <summary>An article of a bot as the API shows it: the format's fields, and the revision to send back to guard an edit.</summary>
public sealed record ArticleView(
    string Id,
    string KnowledgeBase,
    string Language,
    string Title,
    string Content,
    string LlmGuide,
    string Description,
    int Revision
);
