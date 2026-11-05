using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.BotRuntime;

public sealed record FeedArticle
{
    public FeedArticle(Article article, string language)
    {
        ArgumentNullException.ThrowIfNull(article);
        Id = article.Id;
        KnowledgeBase = article.KnowledgeBase;
        Language = language;
        Title = article.Title;
        Description = article.Description;
        Content = article.Content;
        LlmGuide = article.LlmGuide;
    }

    public string Id { get; }

    public string KnowledgeBase { get; }

    public string Language { get; }

    public string Title { get; }

    public string Description { get; }

    public string Content { get; }

    public string LlmGuide { get; }
}
