using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Knowledge;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation.Rules;

public sealed class ArticleLinkRulesTests
{
    private static IEnumerable<Reference> Links(BotDefinition definition) =>
        ReferenceWalker.From(definition).Where(reference => reference.To.Kind == ResourceKind.Article);

    private static readonly KnowledgeBase Offer = new() { Id = "offer", Title = "Offer" };

    private static Article Target(string id, string knowledgeBase = "faq") =>
        Samples.Pricing with
        {
            Id = id,
            KnowledgeBase = knowledgeBase,
        };

    private static BotDefinition Bot(Article from, params Article[] others) =>
        new()
        {
            FormatVersion = 2,
            KnowledgeBases = [Samples.Faq, Offer],
            Articles = [from, .. others],
        };

    private static IReadOnlyList<Mistake> Check(Article from, params Article[] others) =>
        DefinitionValidator.Validate(Bot(from, others), ValidationStage.Save);

    private static Article Linking(string content, string llmGuide = "Ask first.") =>
        Samples.Pricing with
        {
            Id = "wycena",
            Content = content,
            LlmGuide = llmGuide,
        };

    [Fact]
    [Trait("criterion", "AC-article-links-7")]
    public void A_link_to_a_missing_article_is_refused_naming_the_field_and_the_id()
    {
        var mistake = Assert.Single(Check(Linking("See the [Price list](cms://article/cenik)."), Target("cennik")));

        Assert.Equal(MistakeCodes.ReferenceMissing, mistake.Code);
        Assert.Equal(new ResourceRef(ResourceKind.Article, "wycena"), mistake.Resource);
        Assert.Equal("content", mistake.Path);
        Assert.Contains(
            "content of 'wycena' links to article 'cenik', which this bot does not have",
            mistake.Message,
            StringComparison.Ordinal
        );
        Assert.Contains("suwagcms articles list", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-article-links-7")]
    public void The_offline_validator_refuses_the_same_link_in_the_llm_guide_and_reports_every_bad_link_at_once()
    {
        const string json = """
            {"formatVersion":2,"knowledgeBases":[{"id":"faq","title":"Questions"}],"articles":[
            {"id":"wycena","knowledgeBase":"faq","title":"T","content":"[a](cms://article/one) [b](cms://article/one) [c](cms://article/two)","llmGuide":"[d](cms://article/three)","description":"D"}]}
            """;

        var mistakes = DefinitionValidator.ValidateJson(json, ValidationStage.Save);

        Assert.All(mistakes, mistake => Assert.Equal(MistakeCodes.ReferenceMissing, mistake.Code));
        Assert.Equal(["content", "content", "llmGuide"], mistakes.Select(mistake => mistake.Path));
        Assert.Contains("'one'", mistakes[0].Message, StringComparison.Ordinal);
        Assert.Contains("'two'", mistakes[1].Message, StringComparison.Ordinal);
        Assert.Contains("'three'", mistakes[2].Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("criterion", "AC-article-links-9")]
    [InlineData("[x](cms://article/)")]
    [InlineData("[x](cms://page/x)")]
    [InlineData("[x](cms:article/cennik)")]
    [InlineData("[x](cms://article/a/b)")]
    public void A_malformed_cms_target_is_refused_with_the_right_form(string link)
    {
        var mistake = Assert.Single(Check(Linking($"See {link}."), Target("cennik")));

        Assert.Equal(MistakeCodes.ReferenceMalformed, mistake.Code);
        Assert.Equal("content", mistake.Path);
        Assert.Contains("[Title](cms://article/<id>)", mistake.Fix, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("criterion", "AC-article-links-10")]
    [InlineData("A code span `[x](cms://article/nope)` is text.")]
    [InlineData("A double span ``[x](cms://article/nope) with ` inside`` is text.")]
    [InlineData("```\n[x](cms://article/nope)\n```")]
    [InlineData("~~~md\r\n[x](cms://procedure/nope)\r\n~~~")]
    [InlineData("See the price list, article nope, or cms://article/nope as plain words.")]
    [InlineData("An ordinary [web link](https://example.com/article/nope).")]
    public void Code_plain_words_and_web_links_are_not_references(string content)
    {
        var article = Linking(content);

        Assert.Empty(Check(article));
        Assert.Empty(Links(Bot(article)));
    }

    [Fact]
    [Trait("criterion", "AC-article-links-10")]
    public void A_link_after_a_closed_code_block_is_still_a_reference()
    {
        var references = Links(Bot(Linking("```\nx\n```\n[x](cms://article/cennik)"))).ToList();

        Assert.Equal("cennik", Assert.Single(references).To.Id);
    }

    [Fact]
    [Trait("criterion", "AC-article-links-11")]
    public void A_link_to_an_article_in_another_knowledge_base_of_the_bot_is_accepted() =>
        Assert.Empty(Check(Linking("See [Prices](cms://article/cennik)."), Target("cennik", "offer")));

    [Fact]
    [Trait("criterion", "AC-delete-guard-7")]
    public void The_walker_yields_each_article_link_with_its_field_as_path_and_no_fact_use()
    {
        var bot = Bot(Linking("[a](cms://article/one) and [b](cms://article/two)", "[c](cms://article/one)"));

        Assert.Equal(
            [("wycena", "content", "one"), ("wycena", "content", "two"), ("wycena", "llmGuide", "one")],
            Links(bot).Select(reference => (reference.From.Id, reference.Path, reference.To.Id))
        );
        Assert.All(Links(bot), reference => Assert.Null(reference.Use));
    }
}
