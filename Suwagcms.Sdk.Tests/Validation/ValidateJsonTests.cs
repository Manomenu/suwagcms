using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class ValidateJsonTests
{
    [Theory]
    [InlineData("{ not json", "$")]
    [InlineData("", "$")]
    [InlineData("[1]", "$")]
    [InlineData("{\"formatVersion\":1,\"facts\":{}}", "$.facts")]
    [InlineData("{\"formatVersion\":1,\"articles\":[{\"id\":5}]}", "$.articles[0].id")]
    [InlineData("{\"formatVersion\":1,\"articles\":[{}]}", "$.articles[0]")]
    [InlineData("{\"formatVersion\":1,\"actions\":[null]}", "$.actions[0]")]
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
    [InlineData("{\"formatVersion\":2}")]
    [InlineData("{\"formatVersion\":2,\"facts\":{\"wrong\":true}}")]
    [Trait("criterion", "AC-sdk-validator-7")]
    public void A_missing_or_unknown_format_version_in_json_returns_one_version_mistake(string json)
    {
        var mistake = Assert.Single(DefinitionValidator.ValidateJson(json, ValidationStage.Version));

        Assert.Equal(MistakeCodes.FormatVersion, mistake.Code);
        Assert.Contains("reads 1", mistake.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-sdk-validator-10")]
    public void An_unknown_field_is_ignored_and_the_known_fields_are_still_checked()
    {
        const string clean =
            "{\"formatVersion\":1,\"future\":{\"x\":1},\"facts\":[{\"name\":\"a\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3,\"colour\":\"red\"}]}";
        const string duplicated =
            "{\"formatVersion\":1,\"future\":1,\"facts\":[{\"name\":\"a\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3,\"colour\":1},{\"name\":\"a\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3}]}";

        Assert.Empty(DefinitionValidator.ValidateJson(clean, ValidationStage.Version));
        var mistake = Assert.Single(DefinitionValidator.ValidateJson(duplicated, ValidationStage.Version));
        Assert.Equal(new ResourceRef(ResourceKind.GlobalFact, "a"), mistake.Resource);
    }

    [Fact]
    [Trait("criterion", "AC-sdk-validator-2")]
    public void A_json_definition_without_resources_passes() =>
        Assert.Empty(DefinitionValidator.ValidateJson("{\"formatVersion\":1}", ValidationStage.Save));

    [Fact]
    [Trait("criterion", "AC-sdk-validator-1")]
    public void Json_mistakes_from_several_kinds_come_back_together()
    {
        const string json =
            "{\"formatVersion\":1,\"actions\":[{\"id\":\"x\"},{\"id\":\"x\"}],\"knowledgeBases\":[{\"id\":\"k\",\"title\":\"One\"},{\"id\":\"k\",\"title\":\"Two\"}]}";

        var mistakes = DefinitionValidator.ValidateJson(json, ValidationStage.Save);

        Assert.Equal([ResourceKind.KnowledgeBase, ResourceKind.Action], mistakes.Select(mistake => mistake.Resource.Kind));
    }
}
