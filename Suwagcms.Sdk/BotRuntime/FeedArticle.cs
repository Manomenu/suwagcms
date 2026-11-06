using Suwagcms.Sdk.Format.Knowledge;

namespace Suwagcms.Sdk.BotRuntime;

public sealed record FeedArticle
{
    public FeedArticle(Article article, string language, bool isOff)
    {
        ArgumentNullException.ThrowIfNull(article);
        Id = article.Id;
        KnowledgeBase = article.KnowledgeBase;
        Language = language;
        Title = article.Title;
        Description = article.Description;
        Content = article.Content;
        LlmGuide = article.LlmGuide;
        IsOff = isOff;
    }

    public string Id { get; }

    public string KnowledgeBase { get; }

    public string Language { get; }

    public string Title { get; }

    public string Description { get; }

    public string Content { get; }

    public string LlmGuide { get; }

    public bool IsOff { get; }
}
