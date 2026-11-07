using Suwagcms.Sdk.Format.Knowledge;

namespace Suwagcms.Sdk.Tests.Format.Knowledge;

public sealed class ArticleTextTests
{
    private static string? Description(string id) => id == "valuationReport" ? "Orders a valuation report" : null;

    [Fact]
    [Trait("criterion", "AC-article-links-14")]
    public void A_link_to_an_article_becomes_its_label_and_a_link_to_a_procedure_its_description()
    {
        const string markdown = "See [Price list](cms://article/priceList), then [valuationReport](cms://procedure/valuationReport).";

        Assert.Equal("See Price list, then Orders a valuation report.", ArticleText.ForModel(markdown, Description));
    }

    [Fact]
    [Trait("criterion", "AC-article-links-14")]
    public void A_procedure_with_no_description_given_keeps_its_label() =>
        Assert.Equal("Start ghost now.", ArticleText.ForModel("Start [ghost](cms://procedure/ghost) now.", Description));

    [Fact]
    [Trait("criterion", "AC-article-links-14")]
    public void An_address_in_code_an_ordinary_link_and_everything_else_stay_as_written()
    {
        const string markdown =
            "# Fees\n\n`[a](cms://article/a)` and [site](https://example.com)\n\n```\n[b](cms://article/b)\n```\n  * kept ✓ ";

        Assert.Equal(markdown, ArticleText.ForModel(markdown, Description));
    }
}
