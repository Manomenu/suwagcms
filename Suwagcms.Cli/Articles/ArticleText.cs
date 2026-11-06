using System.Globalization;
using Suwagcms.Contracts.Articles;

namespace Suwagcms.Cli.Articles;

internal static class ArticleText
{
    public static string Table(IReadOnlyList<ArticleView> articles)
    {
        ArgumentNullException.ThrowIfNull(articles);
        if (articles.Count == 0)
        {
            return "No articles found. Add one: suwagcms articles add <id> --kb <base> --title <text> --content-file <file> --guide-file <file> --description <text>";
        }
        var idWidth = articles.Max(article => article.Id.Length);
        var titleWidth = articles.Max(article => article.Title.Length);
        var languageWidth = articles.Max(article => article.Language.Length);
        return string.Join(
            Environment.NewLine,
            articles.Select(article =>
                $"{article.Id.PadRight(idWidth)}  {article.Title.PadRight(titleWidth)}  {article.Language.PadRight(languageWidth)}  {article.KnowledgeBase}"
            )
        );
    }

    public static string Detail(ArticleView article)
    {
        ArgumentNullException.ThrowIfNull(article);
        return string.Join(
            Environment.NewLine,
            article.Id,
            $"  knowledge base: {article.KnowledgeBase}",
            $"  language:       {article.Language}",
            $"  title:          {article.Title}",
            $"  description:    {article.Description}",
            string.Create(CultureInfo.InvariantCulture, $"  revision:       {article.Revision}"),
            "  content:",
            Indented(article.Content),
            "  llmGuide:",
            Indented(article.LlmGuide)
        );
    }

    private static string Indented(string text) =>
        string.Join(Environment.NewLine, text.ReplaceLineEndings("\n").Split('\n').Select(line => "    " + line));
}
