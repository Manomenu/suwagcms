using System.Globalization;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Articles;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Sdk.Validation.Rules;

internal static class ArticleRules
{
    public static IReadOnlyList<Rule> All { get; } =
    [
        For(MistakeCode.ArticleIdInvalid, Id),
        For(
            MistakeCode.ArticleTitleMissing,
            article => Blank(article, MistakeCode.ArticleTitleMissing, "title", article.Title, "Say what the article is about.")
        ),
        For(
            MistakeCode.ArticleContentMissing,
            article =>
                Blank(
                    article,
                    MistakeCode.ArticleContentMissing,
                    "content",
                    article.Content,
                    "Write what is true: the customer reads it, through the model."
                )
        ),
        For(
            MistakeCode.ArticleLlmGuideMissing,
            article =>
                Blank(
                    article,
                    MistakeCode.ArticleLlmGuideMissing,
                    "llmGuide",
                    article.LlmGuide,
                    "Say how the model should talk about it: tone, what to ask first, what not to say."
                )
        ),
        For(
            MistakeCode.ArticleDescriptionMissing,
            article =>
                Blank(
                    article,
                    MistakeCode.ArticleDescriptionMissing,
                    "description",
                    article.Description,
                    "Say what the article answers: the bot's search reads it."
                )
        ),
        For(MistakeCode.ArticleContentTooLong, article => TooLong(article, MistakeCode.ArticleContentTooLong, "content", article.Content)),
        For(
            MistakeCode.ArticleLlmGuideTooLong,
            article => TooLong(article, MistakeCode.ArticleLlmGuideTooLong, "llmGuide", article.LlmGuide)
        ),
        new(MistakeCode.ArticleKnowledgeBaseUnknown, ValidationStage.Save, UnknownKnowledgeBase),
    ];

    private static Rule For(MistakeCode code, Func<Article, IEnumerable<Mistake>> check) =>
        new(code, ValidationStage.Save, definition => definition.Articles.SelectMany(check));

    private static IEnumerable<Mistake> Id(Article article)
    {
        if (!ResourceIds.IsCamelCase(article.Id))
        {
            yield return Make(
                MistakeCode.ArticleIdInvalid,
                article,
                "id",
                $"'{article.Id}' is not an article id: it is camelCase, like priceList.",
                $"Rename the article to '{ResourceIds.Suggest(article.Id)}'."
            );
        }
    }

    private static IEnumerable<Mistake> Blank(Article article, MistakeCode code, string path, string text, string fix)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield return Make(code, article, path, $"'{article.Id}' has no {path}.", fix);
        }
    }

    private static IEnumerable<Mistake> TooLong(Article article, MistakeCode code, string path, string text)
    {
        if (text.Length > Article.MaxTextLength)
        {
            yield return Make(
                code,
                article,
                path,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The {path} of '{article.Id}' has {text.Length} characters; the limit is {Article.MaxTextLength}."
                ),
                $"Shorten the {path} to {Article.MaxTextLength} characters or fewer, or split it into articles."
            );
        }
    }

    private static IEnumerable<Mistake> UnknownKnowledgeBase(BotDefinition definition)
    {
        var known = definition.KnowledgeBases.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var bases =
            known.Count == 0
                ? "The bot has no knowledge bases yet: create one first"
                : $"Use one of its knowledge bases: {string.Join(", ", known.Order(StringComparer.Ordinal))}";
        return definition
            .Articles.Where(article => !known.Contains(article.KnowledgeBase))
            .Select(article =>
                Make(
                    MistakeCode.ArticleKnowledgeBaseUnknown,
                    article,
                    "knowledgeBase",
                    string.IsNullOrWhiteSpace(article.KnowledgeBase)
                        ? $"'{article.Id}' is in no knowledge base."
                        : $"'{article.Id}' is in the knowledge base '{article.KnowledgeBase}', which this bot does not have.",
                    $"{bases}. To delete a knowledge base, first move or delete its articles."
                )
            );
    }

    private static Mistake Make(MistakeCode code, Article article, string path, string message, string fix) =>
        new(code, ValidationStage.Save, new ResourceRef(ResourceKind.Article, article.Id), path, message, fix, []);
}
