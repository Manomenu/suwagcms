using Suwagcms.Sdk.Format.Articles;
using Suwagcms.Sdk.Format.KnowledgeBases;

namespace Suwagcms.Sdk.BotRuntime;

public sealed record FeedArticle
{
    public FeedArticle(Article article, KnowledgeBase knowledgeBase, bool isOff)
    {
        ArgumentNullException.ThrowIfNull(article);
        ArgumentNullException.ThrowIfNull(knowledgeBase);
        Id = article.Id;
        KnowledgeBase = article.KnowledgeBase;
        Language = knowledgeBase.Language;
        Embedding = knowledgeBase.Embedding;
        Title = article.Title;
        Description = article.Description;
        Content = article.Content;
        LlmGuide = article.LlmGuide;
        IsOff = isOff;
    }

    public string Id { get; }

    public string KnowledgeBase { get; }

    public string Language { get; }

    public string Embedding { get; }

    public string Title { get; }

    public string Description { get; }

    public string Content { get; }

    public string LlmGuide { get; }

    public bool IsOff { get; }
}
