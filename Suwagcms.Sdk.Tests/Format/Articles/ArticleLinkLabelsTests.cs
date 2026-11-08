using Suwagcms.Sdk.Format.Articles;

namespace Suwagcms.Sdk.Tests.Format.Articles;

public sealed class ArticleLinkLabelsTests
{
    private static readonly Dictionary<string, string> Titles = new() { ["cennik"] = "Price list", ["odd"] = "Odd ] title" };

    private static string Relabel(string text) =>
        ArticleLinkLabels
            .WithCurrentTitles(
                new Article
                {
                    Id = "a",
                    Content = text,
                    LlmGuide = text,
                },
                Titles
            )
            .Content;

    [Theory]
    [InlineData("See [old](cms://article/cennik) now.", "See [Price list](cms://article/cennik) now.")]
    [InlineData(
        "[a](cms://article/cennik)\r\n[b](cms://article/cennik)\r\n",
        "[Price list](cms://article/cennik)\r\n[Price list](cms://article/cennik)\r\n"
    )]
    [InlineData("A [web link](https://example.com) stays.", "A [web link](https://example.com) stays.")]
    [InlineData("A code span `[old](cms://article/cennik)` stays.", "A code span `[old](cms://article/cennik)` stays.")]
    [InlineData(
        "```\n[old](cms://article/cennik)\n```\n[old](cms://article/cennik)",
        "```\n[old](cms://article/cennik)\n```\n[Price list](cms://article/cennik)"
    )]
    [InlineData("An unknown [old](cms://article/nope) stays.", "An unknown [old](cms://article/nope) stays.")]
    [InlineData("A title with a bracket: [old](cms://article/odd).", "A title with a bracket: [old](cms://article/odd).")]
    [Trait("criterion", "AC-article-links-13")]
    public void A_link_to_an_article_takes_its_current_title_and_nothing_else_changes(string text, string expected) =>
        Assert.Equal(expected, Relabel(text));

    [Fact]
    [Trait("criterion", "AC-article-links-13")]
    public void Both_long_texts_are_relabelled()
    {
        var article = ArticleLinkLabels.WithCurrentTitles(
            new Article
            {
                Id = "a",
                Content = "[old](cms://article/cennik)",
                LlmGuide = "[older](cms://article/cennik)",
            },
            Titles
        );

        Assert.Equal("[Price list](cms://article/cennik)", article.Content);
        Assert.Equal("[Price list](cms://article/cennik)", article.LlmGuide);
    }
}
