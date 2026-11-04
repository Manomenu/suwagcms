using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class MistakeShapeTests
{
    private static readonly string[] BadDocuments =
    [
        "",
        "{ not json",
        "[]",
        "{}",
        "{\"formatVersion\":\"x\"}",
        "{\"formatVersion\":3}",
        "{\"formatVersion\":1,\"facts\":[1]}",
        "{\"formatVersion\":1,\"articles\":[{\"id\":\"a\"},{\"id\":\"a\"}]}",
        "{\"formatVersion\":1,\"actions\":[{\"id\":\"a\"},{\"id\":\"a\"}],\"procedures\":[{\"id\":\"p\"},{\"id\":\"p\"}],\"knowledgeBases\":[{\"id\":\"k\"},{\"id\":\"k\"}],\"facts\":[{\"name\":\"f\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3},{\"name\":\"f\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3}]}",
        "{\"formatVersion\":1,\"facts\":[{\"name\":\"Bad Name\",\"type\":\"blob\",\"description\":\" \",\"writableBy\":\"nobody\",\"values\":[\"a\"],\"min\":1},{\"name\":\"w\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3,\"values\":[\"a\"]}]}",
        "{\"formatVersion\":1,\"facts\":[{\"name\":\"kind\",\"type\":\"enum\",\"description\":\"d\",\"values\":[\"a\",\" \",\"a\",\"a\"]},{\"name\":\"count\",\"type\":\"int\",\"description\":\"d\",\"min\":5},{\"name\":\"range\",\"type\":\"int\",\"description\":\"d\",\"min\":5,\"max\":1},{\"name\":\"single\",\"type\":\"enum\",\"description\":\"d\",\"values\":[\"a\"]},{\"name\":\"note\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":0}]}",
    ];

    [Fact]
    [Trait("criterion", "AC-sdk-validator-3")]
    public void Every_mistake_of_every_rule_is_complete()
    {
        var mistakes = BadDocuments.SelectMany(json => DefinitionValidator.ValidateJson(json, ValidationStage.Version)).ToList();
        var rulesSeen = DefinitionValidator.Rules.Select(rule => rule.Code).ToList();

        Assert.All(mistakes, AssertComplete);
        Assert.Equal(
            [.. rulesSeen.Append(MistakeCodes.FormatInvalid).Append(MistakeCodes.FormatVersion).Order(StringComparer.Ordinal)],
            mistakes.Select(mistake => mistake.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
        );
        Assert.All(rulesSeen, code => Assert.Contains(mistakes, mistake => mistake.Code == code));
    }

    private static void AssertComplete(Mistake mistake)
    {
        Assert.False(string.IsNullOrWhiteSpace(mistake.Code));
        Assert.NotNull(mistake.Resource);
        Assert.NotNull(mistake.Resource.Id);
        Assert.False(string.IsNullOrWhiteSpace(mistake.Path));
        Assert.False(string.IsNullOrWhiteSpace(mistake.Message));
        Assert.False(string.IsNullOrWhiteSpace(mistake.Fix));
        Assert.NotNull(mistake.Related);
        Assert.True(Enum.IsDefined(mistake.Stage));
    }
}
