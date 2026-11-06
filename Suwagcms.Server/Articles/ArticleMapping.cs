using Suwagcms.Contracts.Articles;
using Suwagcms.Sdk.Format.Knowledge;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Articles;

internal static class ArticleMapping
{
    public static Article ToRecord(string id, SaveArticle body) =>
        new()
        {
            Id = id,
            KnowledgeBase = body.KnowledgeBase ?? "",
            Title = body.Title ?? "",
            Content = body.Content ?? "",
            LlmGuide = body.LlmGuide ?? "",
            Description = body.Description ?? "",
        };

    // Why: an article has no language of its own, so it is read off its knowledge base, which the stored resources hold.
    public static Dictionary<string, string> Languages(IEnumerable<StoredResource> stored) =>
        stored
            .Where(item => item.Target.Kind == ResourceKind.KnowledgeBase)
            .ToDictionary(item => item.Target.Id, item => ((KnowledgeBase)item.Record).Language, StringComparer.Ordinal);

    public static string LanguageOf(IReadOnlyDictionary<string, string> languages, Article article) =>
        languages.GetValueOrDefault(article.KnowledgeBase, KnowledgeBaseLanguages.English);

    public static ArticleView Show(StoredResource stored, IReadOnlyDictionary<string, string> languages) =>
        Show((Article)stored.Record, stored.Revision, languages);

    public static ArticleView Show(Article article, int revision, IReadOnlyDictionary<string, string> languages) =>
        new(
            article.Id,
            article.KnowledgeBase,
            LanguageOf(languages, article),
            article.Title,
            article.Content,
            article.LlmGuide,
            article.Description,
            revision
        );

    // Why: the text filter is for authors finding an article they know, so it reads the title, id and description and never the content.
    public static bool Matches(
        Article article,
        string? knowledgeBase,
        string? text,
        string? language,
        IReadOnlyDictionary<string, string> languages
    ) =>
        (string.IsNullOrEmpty(knowledgeBase) || article.KnowledgeBase == knowledgeBase)
        && (string.IsNullOrEmpty(language) || LanguageOf(languages, article) == language)
        && (
            string.IsNullOrWhiteSpace(text)
            || article.Title.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)
            || article.Id.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)
            || article.Description.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)
        );
}
