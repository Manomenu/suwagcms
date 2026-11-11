using Suwagcms.Sdk.Format.Articles;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Sdk.Tests.Format.Articles;

public sealed class LinkRewriteTests
{
    private static string Rewritten(string text) =>
        ReferenceWalker
            .Rewrite(
                new Article
                {
                    Id = "other",
                    Content = text,
                    LlmGuide = text,
                },
                new ResourceRef(ResourceKind.Article, "cennik"),
                "priceList"
            )
            .Content;

    [Theory]
    [Trait("criterion", "AC-resource-rename-3")]
    [InlineData("See the [Price list](cms://article/cennik).", "See the [Price list](cms://article/priceList).")]
    [InlineData("[a](cms://article/cennik) and [b](cms://article/cennik)", "[a](cms://article/priceList) and [b](cms://article/priceList)")]
    [InlineData("[a](<cms://article/cennik> \"T\")", "[a](<cms://article/priceList> \"T\")")]
    [InlineData("[cennik](cms://article/cennik)", "[cennik](cms://article/priceList)")]
    [InlineData("line\r\n\r\n  [a](cms://article/cennik)  \r\nend\r\n", "line\r\n\r\n  [a](cms://article/priceList)  \r\nend\r\n")]
    [InlineData("```\ncode\n```\n\n[a](cms://article/cennik)\n", "```\ncode\n```\n\n[a](cms://article/priceList)\n")]
    public void Only_the_target_of_a_link_changes(string before, string after) => Assert.Equal(after, Rewritten(before));

    [Theory]
    [Trait("criterion", "AC-resource-rename-3")]
    [InlineData("`[a](cms://article/cennik)`")]
    [InlineData("```\n[a](cms://article/cennik)\n```\n")]
    [InlineData("~~~\r\n[a](cms://article/cennik)\r\n~~~\r\n")]
    [InlineData("the word cennik and cms://article/cennik in plain text")]
    [InlineData("[a](cms://article/cennikFirmowy)")]
    [InlineData("[a](https://example.com/cms://article/cennik)")]
    [InlineData("[a](https://example.com/cennik)")]
    [InlineData("\\[a](cms://article/cennik)")]
    public void Text_in_code_plain_words_longer_ids_and_web_links_stay_byte_for_byte(string text) => Assert.Equal(text, Rewritten(text));

    [Fact]
    [Trait("criterion", "AC-resource-rename-1")]
    public void Both_fields_of_an_article_are_rewritten_and_the_walker_finds_no_link_to_the_old_id()
    {
        var article = new Article
        {
            Id = "other",
            Content = "[a](cms://article/cennik)",
            LlmGuide = "[b](cms://article/cennik)",
        };
        var target = new ResourceRef(ResourceKind.Article, "cennik");

        var rewritten = ReferenceWalker.Rewrite(article, target, "priceList");

        Assert.DoesNotContain(ReferenceWalker.From(rewritten), reference => reference.To == target);
        Assert.Equal(2, ReferenceWalker.From(rewritten).Count(reference => reference.To.Id == "priceList"));
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-18")]
    public void A_facts_name_is_rewritten_wherever_the_walker_finds_it_and_a_local_fact_of_that_name_is_left_alone()
    {
        var procedure = new Procedure
        {
            Id = "p",
            Steps = new Dictionary<string, ProcedureStep>
            {
                ["a"] = new() { Collect = ["customer.city", "other"] },
                ["b"] = new() { Assign = new Dictionary<string, string> { ["customer.city"] = "Warsaw" } },
                ["c"] = new()
                {
                    Say = "customer.city",
                    Next = new NextSteps
                    {
                        Rules =
                        [
                            new Transition
                            {
                                When = new Dictionary<string, Condition> { ["customer.city"] = new() { Known = true } },
                                To = "a",
                            },
                            new Transition { To = "b" },
                        ],
                    },
                },
            },
        };
        var target = new ResourceRef(ResourceKind.GlobalFact, "customer.city");

        var rewritten = ReferenceWalker.Rewrite(procedure, target, "client.town");

        Assert.DoesNotContain(ReferenceWalker.From(rewritten), reference => reference.To == target);
        Assert.Equal(3, ReferenceWalker.From(rewritten).Count(reference => reference.To.Id == "client.town"));
        Assert.Equal("customer.city", rewritten.Steps["c"].Say);
        Assert.Equal(["client.town", "other"], rewritten.Steps["a"].Collect);

        var local = procedure with { LocalFacts = new Dictionary<string, LocalFact> { ["customer.city"] = new() } };
        Assert.Same(local, ReferenceWalker.Rewrite(local, target, "client.town"));
    }

    [Fact]
    [Trait("criterion", "AC-resource-rename-30")]
    public void A_facts_name_is_rewritten_in_each_whole_say_placeholder_and_nowhere_else_in_the_text()
    {
        const string Text = "Booked {customer.city}; {customer.cityName}, customer.city, { customer.city } and {customer.city}.";
        var procedure = new Procedure
        {
            Id = "p",
            Steps = new Dictionary<string, ProcedureStep> { ["tell"] = new() { Say = Text } },
        };
        var target = new ResourceRef(ResourceKind.GlobalFact, "customer.city");

        var rewritten = ReferenceWalker.Rewrite(procedure, target, "client.town");

        Assert.Equal(
            "Booked {client.town}; {customer.cityName}, customer.city, { customer.city } and {client.town}.",
            rewritten.Steps["tell"].Say
        );
        Assert.Equal(
            ["client.town", "customer.cityName"],
            ReferenceWalker.From(rewritten).Select(reference => reference.To.Id).Order(StringComparer.Ordinal)
        );
        var local = procedure with { LocalFacts = new Dictionary<string, LocalFact> { ["customer.city"] = new() } };
        Assert.Equal(Text, ReferenceWalker.Rewrite(local, target, "client.town").Steps["tell"].Say);
    }
}
