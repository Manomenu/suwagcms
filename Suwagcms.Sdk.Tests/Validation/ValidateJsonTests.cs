using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class ValidateJsonTests
{
    [Theory]
    [InlineData("{ not json", "$")]
    [InlineData("", "$")]
    [InlineData("[1]", "$")]
    [InlineData("{\"formatVersion\":3,\"facts\":{}}", "$.facts")]
    [InlineData("{\"formatVersion\":3,\"articles\":[{\"id\":5}]}", "$.articles[0].id")]
    [InlineData("{\"formatVersion\":3,\"articles\":[{}]}", "$.articles[0]")]
    [InlineData("{\"formatVersion\":3,\"actions\":[null]}", "$.actions[0]")]
    [Trait("criterion", "AC-sdk-validator-9")]
    public void Bad_input_returns_a_mistake_with_a_path_and_never_throws(string json, string path)
    {
        var mistake = Assert.Single(DefinitionValidator.ValidateJson(json, ValidationStage.Version));

        Assert.Equal(MistakeCodes.FormatInvalid, mistake.Code);
        Assert.Equal(path, mistake.Path);
    }

    [Theory]
    [InlineData("{\"facts\":[]}")]
    [InlineData("{\"formatVersion\":\"1\"}")]
    [InlineData("{\"formatVersion\":4}")]
    [InlineData("{\"formatVersion\":4,\"facts\":{\"wrong\":true}}")]
    [Trait("criterion", "AC-sdk-validator-7")]
    public void A_missing_or_unknown_format_version_in_json_returns_one_version_mistake(string json)
    {
        var mistake = Assert.Single(DefinitionValidator.ValidateJson(json, ValidationStage.Version));

        Assert.Equal(MistakeCodes.FormatVersion, mistake.Code);
        Assert.Contains("reads 3", mistake.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-procedure-creator-46")]
    public void The_old_format_is_refused_with_one_version_mistake_whose_fix_says_what_to_do()
    {
        var mistake = Assert.Single(DefinitionValidator.ValidateJson("{\"formatVersion\":1,\"facts\":[]}", ValidationStage.Version));

        Assert.Equal(MistakeCodes.FormatVersion, mistake.Code);
        Assert.Contains("set formatVersion to 3", mistake.Fix, StringComparison.Ordinal);
        Assert.Contains("create a new version from the working state", mistake.Fix, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-sdk-validator-10")]
    public void An_unknown_field_is_ignored_and_the_known_fields_are_still_checked()
    {
        const string clean =
            "{\"formatVersion\":3,\"future\":{\"x\":1},\"facts\":[{\"name\":\"a\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3,\"colour\":\"red\"}]}";
        const string duplicated =
            "{\"formatVersion\":3,\"future\":1,\"facts\":[{\"name\":\"a\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3,\"colour\":1},{\"name\":\"a\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3}]}";

        Assert.Empty(DefinitionValidator.ValidateJson(clean, ValidationStage.Save));
        var mistake = Assert.Single(DefinitionValidator.ValidateJson(duplicated, ValidationStage.Save));
        Assert.Equal(new ResourceRef(ResourceKind.GlobalFact, "a"), mistake.Resource);
    }

    [Fact]
    [Trait("criterion", "AC-sdk-validator-2")]
    public void A_json_definition_without_resources_passes() =>
        Assert.Empty(DefinitionValidator.ValidateJson("{\"formatVersion\":3}", ValidationStage.Save));

    [Fact]
    [Trait("criterion", "AC-sdk-validator-1")]
    public void Json_mistakes_from_several_kinds_come_back_together()
    {
        const string json =
            "{\"formatVersion\":3,\"actions\":[{\"id\":\"x\"},{\"id\":\"x\"}],\"knowledgeBases\":[{\"id\":\"k\",\"title\":\"One\",\"embedding\":\"default\",\"results\":5},{\"id\":\"k\",\"title\":\"Two\",\"embedding\":\"default\",\"results\":5}]}";

        var mistakes = DefinitionValidator.ValidateJson(json, ValidationStage.Save);

        Assert.Equal(
            [ResourceKind.KnowledgeBase, ResourceKind.Action],
            mistakes.Where(mistake => mistake.Code == MistakeCodes.DuplicateId).Select(mistake => mistake.Resource.Kind)
        );
    }
}
