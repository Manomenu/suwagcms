using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Articles;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;
using Suwagcms.Sdk.Validation.Rules;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class DefinitionValidatorTests
{
    private static readonly Rule VersionRule = new(
        "test.versionOnly",
        ValidationStage.Version,
        definition =>
            definition.Articles.Select(article => new Mistake(
                "test.versionOnly",
                ValidationStage.Save,
                new ResourceRef(ResourceKind.Article, article.Id),
                "id",
                "m",
                "f",
                []
            ))
    );

    [Fact]
    [Trait("criterion", "AC-sdk-validator-1")]
    public void Mistakes_in_different_resources_come_back_in_one_result()
    {
        var mistakes = DefinitionValidator.Validate(Samples.WithDuplicatesInTwoKinds, ValidationStage.Save);

        Assert.Equal(
            [(ResourceKind.GlobalFact, "customer.city"), (ResourceKind.Procedure, "quote")],
            mistakes.Select(mistake => (mistake.Resource.Kind, mistake.Resource.Id))
        );
    }

    [Theory]
    [InlineData(ValidationStage.Save)]
    [InlineData(ValidationStage.Version)]
    [Trait("criterion", "AC-sdk-validator-2")]
    public void A_definition_without_mistakes_returns_an_empty_list(ValidationStage stage)
    {
        Assert.Empty(DefinitionValidator.Validate(Samples.Valid, stage));
        Assert.Empty(DefinitionValidator.Validate(Samples.Empty, ValidationStage.Save));
    }

    [Fact]
    [Trait("criterion", "AC-sdk-validator-4")]
    public void The_same_definition_gives_the_same_mistakes_in_the_same_order()
    {
        var shuffled = new BotDefinition
        {
            FormatVersion = 5,
            Procedures =
            [
                Samples.Procedure("conversation"),
                Samples.Procedure("quote"),
                Samples.Procedure("other"),
                Samples.Procedure("quote"),
            ],
            Facts = [Samples.CityFact, Samples.CityFact],
        };

        var first = DefinitionValidator.Validate(Samples.WithDuplicatesInTwoKinds, ValidationStage.Version);
        var second = DefinitionValidator.Validate(Samples.WithDuplicatesInTwoKinds, ValidationStage.Version);

        Assert.Equal(2, first.Count);
        Assert.Equal(first.Select(Describe), second.Select(Describe));
        Assert.Equal(first.Select(Describe), DefinitionValidator.Validate(shuffled, ValidationStage.Version).Select(Describe));
    }

    [Fact]
    [Trait("criterion", "AC-sdk-validator-5")]
    public void Save_reports_only_save_mistakes_and_version_reports_both_each_with_its_own_stage()
    {
        var definition = new BotDefinition
        {
            FormatVersion = 5,
            Articles = [new Article { Id = "a" }],
            Procedures = [new Procedure { Id = "p" }, new Procedure { Id = "p" }],
        };
        Rule[] rules = [DuplicateIdRuleFor(), VersionRule];

        var atSave = DefinitionValidator.Validate(definition, ValidationStage.Save, rules);
        var atVersion = DefinitionValidator.Validate(definition, ValidationStage.Version, rules);

        Assert.Equal([("resource.duplicateId", ValidationStage.Save)], atSave.Select(mistake => (mistake.Code, mistake.Stage)));
        Assert.Equal(
            [("resource.duplicateId", ValidationStage.Save), ("test.versionOnly", ValidationStage.Version)],
            atVersion.Select(mistake => (mistake.Code, mistake.Stage))
        );
    }

    [Fact]
    [Trait("criterion", "AC-sdk-validator-7")]
    public void An_unknown_format_version_returns_one_mistake_and_no_rule_runs()
    {
        var definition = Samples.WithDuplicatesInTwoKinds with { FormatVersion = 4 };

        var mistake = Assert.Single(DefinitionValidator.Validate(definition, ValidationStage.Version));

        Assert.Equal(MistakeCodes.FormatVersion, mistake.Code);
        Assert.Contains("reads 5", mistake.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-sdk-validator-8")]
    public void A_duplicate_id_names_the_id_and_both_places()
    {
        var mistake = Assert.Single(
            DefinitionValidator.Validate(
                new BotDefinition
                {
                    FormatVersion = 5,
                    Articles = [new Article { Id = "pricing" }, new Article { Id = "faq" }, new Article { Id = "pricing" }],
                },
                ValidationStage.Save,
                [DuplicateIdRuleFor()]
            )
        );

        Assert.Equal(MistakeCodes.DuplicateId, mistake.Code);
        Assert.Equal(new ResourceRef(ResourceKind.Article, "pricing"), mistake.Resource);
        Assert.Contains("'pricing'", mistake.Message, StringComparison.Ordinal);
        Assert.Equal(["articles[0]", "articles[2]"], mistake.Related.Select(related => related.Location));
    }

    private static Rule DuplicateIdRuleFor() => Assert.Single(DefinitionValidator.Rules, rule => rule.Code == MistakeCodes.DuplicateId);

    private static string Describe(Mistake mistake) =>
        $"{mistake.Code}|{mistake.Stage}|{mistake.Resource}|{mistake.Path}|{string.Join(',', mistake.Related.Select(related => related.Location))}";
}
