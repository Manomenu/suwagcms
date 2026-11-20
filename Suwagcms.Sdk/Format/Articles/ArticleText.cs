namespace Suwagcms.Sdk.Format.Articles;

public static class ArticleText
{
    public static string ForModel(string markdown, Func<string, string?> procedureDescription)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(procedureDescription);
        return CmsLinks.ForModel(markdown, procedureDescription);
    }
}
