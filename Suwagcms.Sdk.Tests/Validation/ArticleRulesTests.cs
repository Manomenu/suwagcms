using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class ArticleRulesTests
{
    private static IReadOnlyList<Mistake> Check(params Article[] articles) =>
        DefinitionValidator.Validate(
            new BotDefinition
            {
                FormatVersion = 1,
                KnowledgeBases = [Samples.Faq, new KnowledgeBase { Id = "offer", Title = "Offer" }],
                Articles = articles,
            },
            ValidationStage.Save
        );

    private static string[] Codes(params Article[] articles) => [.. Check(articles).Select(mistake => mistake.Code)];

    [Fact]
    [Trait("criterion", "AC-articles-1")]
    public void A_complete_article_is_accepted_in_any_base_of_the_bot() =>
        Assert.Empty(Check(Samples.Pricing, Samples.Pricing with { Id = "other", KnowledgeBase = "offer" }));

    [Theory]
    [Trait("criterion", "AC-articles-2")]
    [InlineData("title", MistakeCodes.ArticleTitleMissing)]
    [InlineData("content", MistakeCodes.ArticleContentMissing)]
    [InlineData("llmGuide", MistakeCodes.ArticleLlmGuideMissing)]
    [InlineData("description", MistakeCodes.ArticleDescriptionMissing)]
    public void A_blank_title_content_guide_or_description_is_refused_for_that_field_with_a_fix(string field, string code)
    {
        foreach (var blank in new[] { "", "  \n " })
        {
            var article = field switch
            {
                "title" => Samples.Pricing with { Title = blank },
                "content" => Samples.Pricing with { Content = blank },
                "llmGuide" => Samples.Pricing with { LlmGuide = blank },
                _ => Samples.Pricing with { Description = blank },
            };

            var mistake = Assert.Single(Check(article));

            Assert.Equal(code, mistake.Code);
            Assert.Equal(field, mistake.Path);
            Assert.False(string.IsNullOrWhiteSpace(mistake.Fix));
        }
    }

    [Fact]
    [Trait("criterion", "AC-articles-2")]
    public void Every_missing_text_is_reported_at_once()
    {
        var mistakes = Check(new Article { Id = "pricing", KnowledgeBase = "faq" });

        Assert.Equal(
            [
                MistakeCodes.ArticleContentMissing,
                MistakeCodes.ArticleDescriptionMissing,
                MistakeCodes.ArticleLlmGuideMissing,
                MistakeCodes.ArticleTitleMissing,
            ],
            mistakes.Select(mistake => mistake.Code).Order(StringComparer.Ordinal)
        );
    }

    [Theory]
    [Trait("criterion", "AC-articles-3")]
    [InlineData("")]
    [InlineData("Price list")]
    [InlineData("price_list")]
    public void A_missing_or_non_camelCase_id_is_refused_with_the_id_to_use(string id)
    {
        var mistake = Assert.Single(Check(Samples.Pricing with { Id = id }));

        Assert.Equal(MistakeCodes.ArticleIdInvalid, mistake.Code);
        Assert.Contains("Rename the article to '", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-articles-3")]
    public void A_knowledge_base_the_bot_does_not_have_is_refused_listing_the_bases_it_has()
    {
        var mistake = Assert.Single(Check(Samples.Pricing with { KnowledgeBase = "nowhere" }));

        Assert.Equal(MistakeCodes.ArticleKnowledgeBaseUnknown, mistake.Code);
        Assert.Equal("knowledgeBase", mistake.Path);
        Assert.Contains("'nowhere'", mistake.Message, StringComparison.Ordinal);
        Assert.Contains("faq, offer", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-articles-3")]
    public void An_article_in_no_base_is_refused_and_a_bot_with_no_bases_is_told_to_create_one()
    {
        Assert.Equal([MistakeCodes.ArticleKnowledgeBaseUnknown], Codes(Samples.Pricing with { KnowledgeBase = "" }));
        var mistake = Assert.Single(
            DefinitionValidator.Validate(new BotDefinition { FormatVersion = 1, Articles = [Samples.Pricing] }, ValidationStage.Save)
        );
        Assert.Contains("create one first", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-articles-12")]
    public void A_text_at_the_limit_is_accepted_and_one_character_over_is_refused_with_the_limit_and_the_length()
    {
        Assert.Empty(
            Check(
                Samples.Pricing with
                {
                    Content = new string('x', Article.MaxTextLength),
                    LlmGuide = new string('y', Article.MaxTextLength),
                }
            )
        );

        var mistakes = Check(
            Samples.Pricing with
            {
                Content = new string('x', Article.MaxTextLength + 1),
                LlmGuide = new string('y', Article.MaxTextLength + 5),
            }
        );

        Assert.Equal(
            [MistakeCodes.ArticleContentTooLong, MistakeCodes.ArticleLlmGuideTooLong],
            mistakes.Select(mistake => mistake.Code).Order(StringComparer.Ordinal)
        );
        var content = Assert.Single(mistakes, mistake => mistake.Path == "content");
        Assert.Contains("has 200001 characters; the limit is 200000", content.Message, StringComparison.Ordinal);
        Assert.Contains("200000", content.Fix, StringComparison.Ordinal);
        Assert.Contains("200005", Assert.Single(mistakes, mistake => mistake.Path == "llmGuide").Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-articles-4")]
    public void The_validator_does_not_change_the_text_of_an_article()
    {
        var text = "# Cennik\r\n\r\n- zażółć gęślą jaźń\n\n";
        var article = Samples.Pricing with { Content = text, LlmGuide = text };

        Assert.Empty(Check(article));
        Assert.Equal(text, article.Content);
    }

    [Fact]
    [Trait("criterion", "AC-article-language-1")]
    public void An_article_without_a_language_has_en()
    {
        Assert.Equal("en", new Article { Id = "pricing" }.Language);
        Assert.Equal("en", Samples.Pricing.Language);
    }

    [Theory]
    [Trait("criterion", "AC-article-language-4")]
    [InlineData("pl")]
    [InlineData("en")]
    public void The_two_languages_are_accepted(string language) => Assert.Empty(Check(Samples.Pricing with { Language = language }));

    [Theory]
    [Trait("criterion", "AC-article-language-4")]
    [InlineData("PL")]
    [InlineData("En")]
    [InlineData("de")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("pl ")]
    public void Any_other_language_including_a_different_case_and_blank_is_refused_listing_both(string language)
    {
        var mistake = Assert.Single(Check(Samples.Pricing with { Language = language }));

        Assert.Equal(MistakeCodes.ArticleLanguageUnknown, mistake.Code);
        Assert.Equal("language", mistake.Path);
        Assert.Equal("Use one of: pl, en.", mistake.Fix);
        Assert.Contains("'pricing'", mistake.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-article-language-4")]
    public void The_message_names_the_language_given()
    {
        var mistake = Assert.Single(Check(Samples.Pricing with { Id = "cennik", Language = "de" }));

        Assert.Equal("'cennik' has language 'de'.", mistake.Message);
    }

    [Fact]
    [Trait("criterion", "AC-article-language-4")]
    public void A_json_definition_with_another_language_fails_offline_as_it_does_on_the_server()
    {
        const string json =
            """{"formatVersion":1,"knowledgeBases":[{"id":"faq","title":"Questions"}],"articles":[{"id":"cennik","knowledgeBase":"faq","title":"T","content":"c","llmGuide":"g","description":"d","language":"de"}]}""";

        var mistake = Assert.Single(DefinitionValidator.ValidateJson(json, ValidationStage.Save));

        Assert.Equal(MistakeCodes.ArticleLanguageUnknown, mistake.Code);
    }

    [Fact]
    [Trait("criterion", "AC-article-language-1")]
    public void A_json_article_without_a_language_reads_as_en_and_is_accepted()
    {
        const string json =
            """{"formatVersion":1,"knowledgeBases":[{"id":"faq","title":"Questions"}],"articles":[{"id":"cennik","knowledgeBase":"faq","title":"T","content":"c","llmGuide":"g","description":"d"}]}""";

        Assert.Empty(DefinitionValidator.ValidateJson(json, ValidationStage.Save));
    }

    [Fact]
    [Trait("criterion", "AC-article-language-7")]
    public void Two_articles_in_different_languages_can_share_a_title()
    {
        var polish = Samples.Pricing with { Id = "cennik", Language = "pl", Title = "Cennik" };
        var english = Samples.Pricing with { Id = "pricing", Language = "en", Title = "Cennik" };

        Assert.Empty(Check(polish, english));
    }
}
