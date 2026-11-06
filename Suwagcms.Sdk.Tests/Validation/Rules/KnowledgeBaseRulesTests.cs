using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Knowledge;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation.Rules;

public sealed class KnowledgeBaseRulesTests
{
    private static readonly KnowledgeBase Offer = new() { Id = "offer", Title = "Our offer" };

    private static IReadOnlyList<Mistake> Check(params KnowledgeBase[] bases) =>
        DefinitionValidator.Validate(new BotDefinition { FormatVersion = 2, KnowledgeBases = bases }, ValidationStage.Save);

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-4")]
    public void A_base_with_an_id_and_a_title_is_accepted() =>
        Assert.Empty(Check(Offer, new KnowledgeBase { Id = "forValuers", Title = "For valuers" }));

    [Theory]
    [Trait("criterion", "AC-knowledge-bases-4")]
    [InlineData("")]
    [InlineData("Offer")]
    [InlineData("for_valuers")]
    [InlineData("for valuers")]
    [InlineData("1offer")]
    public void A_missing_or_non_camelCase_id_is_refused_with_the_id_to_use(string id)
    {
        var mistake = Assert.Single(Check(Offer with { Id = id }));

        Assert.Equal(MistakeCodes.KnowledgeBaseIdInvalid, mistake.Code);
        Assert.Equal("id", mistake.Path);
        Assert.Contains("Rename the knowledge base to '", mistake.Fix, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("criterion", "AC-knowledge-bases-4")]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_title_is_refused(string title)
    {
        var mistake = Assert.Single(Check(Offer with { Title = title }));

        Assert.Equal(MistakeCodes.KnowledgeBaseTitleMissing, mistake.Code);
        Assert.Equal("title", mistake.Path);
    }

    [Theory]
    [Trait("criterion", "AC-knowledge-bases-4")]
    [InlineData("OUR OFFER")]
    [InlineData("  our offer ")]
    public void A_title_another_base_has_is_refused_for_both_bases_ignoring_case_and_surrounding_spaces(string title)
    {
        var mistakes = Check(Offer, new KnowledgeBase { Id = "second", Title = title });

        Assert.Equal(
            [MistakeCodes.KnowledgeBaseTitleRepeated, MistakeCodes.KnowledgeBaseTitleRepeated],
            mistakes.Select(mistake => mistake.Code)
        );
        Assert.Equal(["offer", "second"], mistakes.Select(mistake => mistake.Resource.Id));
        Assert.Contains("used by knowledge base 'offer'", mistakes[1].Message, StringComparison.Ordinal);
        Assert.Equal(["offer"], mistakes[1].Related.Select(related => related.Resource.Id));
        Assert.Contains("Choose another title", mistakes[1].Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-4")]
    public void Two_blank_titles_are_reported_as_missing_and_not_as_repeated() =>
        Assert.Equal(
            [MistakeCodes.KnowledgeBaseTitleMissing, MistakeCodes.KnowledgeBaseTitleMissing],
            Check(Offer with { Title = "" }, new KnowledgeBase { Id = "second", Title = " " }).Select(mistake => mistake.Code)
        );

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-1")]
    public void A_base_without_a_language_has_en_and_a_json_base_without_one_reads_as_en_and_is_accepted()
    {
        const string json = """{"formatVersion":2,"knowledgeBases":[{"id":"faq","title":"Questions"}]}""";

        Assert.Equal("en", Offer.Language);
        Assert.Equal("en", new KnowledgeBase { Id = "faq" }.Language);
        Assert.Empty(DefinitionValidator.ValidateJson(json, ValidationStage.Save));
    }

    [Theory]
    [Trait("criterion", "AC-knowledge-base-language-4")]
    [InlineData("pl")]
    [InlineData("en")]
    public void The_two_languages_are_accepted(string language) => Assert.Empty(Check(Offer with { Language = language }));

    [Theory]
    [Trait("criterion", "AC-knowledge-base-language-4")]
    [InlineData("PL")]
    [InlineData("En")]
    [InlineData("de")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("pl ")]
    public void Any_other_language_including_a_different_case_and_blank_is_refused_listing_both(string language)
    {
        var mistake = Assert.Single(Check(Offer with { Language = language }));

        Assert.Equal("knowledgeBase.language.unknown", mistake.Code);
        Assert.Equal(ValidationStage.Save, mistake.Stage);
        Assert.Equal("language", mistake.Path);
        Assert.Equal("Use one of: pl, en.", mistake.Fix);
        Assert.Contains("'offer'", mistake.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-4")]
    public void The_message_names_the_language_given_and_a_json_definition_with_another_language_fails_offline()
    {
        const string json = """{"formatVersion":2,"knowledgeBases":[{"id":"oferta","title":"Oferta","language":"de"}]}""";

        var mistake = Assert.Single(DefinitionValidator.ValidateJson(json, ValidationStage.Save));

        Assert.Equal(MistakeCodes.KnowledgeBaseLanguageUnknown, mistake.Code);
        Assert.Equal("Knowledge base 'oferta' has language 'de'.", mistake.Message);
    }
}
