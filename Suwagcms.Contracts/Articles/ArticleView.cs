namespace Suwagcms.Contracts.Articles;

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
