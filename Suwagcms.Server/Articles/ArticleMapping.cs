using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Articles;

internal static class ArticleMapping
{
    public static Article ToRecord(string id, SaveArticle body, string language) =>
        new()
        {
            Id = id,
            KnowledgeBase = body.KnowledgeBase ?? "",
            Language = body.Language ?? language,
            Title = body.Title ?? "",
            Content = body.Content ?? "",
            LlmGuide = body.LlmGuide ?? "",
            Description = body.Description ?? "",
        };

    public static ArticleView Show(StoredResource stored) => Show((Article)stored.Record, stored.Revision);

    public static ArticleView Show(Article article, int revision) =>
        new(
            article.Id,
            article.KnowledgeBase,
            article.Language,
            article.Title,
            article.Content,
            article.LlmGuide,
            article.Description,
            revision
        );

    // Why: the text filter is for authors finding an article they know, so it reads the title, id and description and never the content.
    public static bool Matches(Article article, string? knowledgeBase, string? text, string? language) =>
        (string.IsNullOrEmpty(knowledgeBase) || article.KnowledgeBase == knowledgeBase)
        && (string.IsNullOrEmpty(language) || article.Language == language)
        && (
            string.IsNullOrWhiteSpace(text)
            || article.Title.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)
            || article.Id.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)
            || article.Description.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)
        );
}
