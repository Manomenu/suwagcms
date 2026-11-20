namespace Suwagcms.Sdk.Format.Articles;

public static class ArticleLinkLabels
{
    public static Article WithCurrentTitles(Article article, IReadOnlyDictionary<string, string> titles)
    {
        ArgumentNullException.ThrowIfNull(article);
        ArgumentNullException.ThrowIfNull(titles);
        return article with { Content = CmsLinks.Relabel(article.Content, titles), LlmGuide = CmsLinks.Relabel(article.LlmGuide, titles) };
    }
}
