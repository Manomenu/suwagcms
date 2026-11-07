namespace Suwagcms.Sdk.Format.Knowledge;

// Why: a bot gives an article's text to a model, and only the SDK reads cms:// links, so a bot needs no second reader of them.
public static class ArticleText
{
    // Why: a link to a procedure is written for the guide, and its label there is an id, so the model reads the procedure's description instead; null from the lookup keeps the label.
    public static string ForModel(string markdown, Func<string, string?> procedureDescription)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(procedureDescription);
        return CmsLinks.ForModel(markdown, procedureDescription);
    }
}
