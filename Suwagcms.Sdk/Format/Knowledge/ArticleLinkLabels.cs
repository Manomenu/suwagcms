namespace Suwagcms.Sdk.Format.Knowledge;

// Why: a stored label is the title when the link was written, and a version carries current titles, so a retitled article never reaches a customer under its old name.
public static class ArticleLinkLabels
{
    public static Article WithCurrentTitles(Article article, IReadOnlyDictionary<string, string> titles)
    {
        ArgumentNullException.ThrowIfNull(article);
        ArgumentNullException.ThrowIfNull(titles);
        return article with { Content = CmsLinks.Relabel(article.Content, titles), LlmGuide = CmsLinks.Relabel(article.LlmGuide, titles) };
    }
}
