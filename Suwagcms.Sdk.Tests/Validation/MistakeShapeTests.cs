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
        "{\"formatVersion\":1,\"actions\":[{\"id\":\"a\"},{\"id\":\"a\"}],\"procedures\":[{\"id\":\"p\"},{\"id\":\"p\"}],\"knowledgeBases\":[{\"id\":\"k\"},{\"id\":\"k\"}],\"facts\":[{\"name\":\"f\"},{\"name\":\"f\"}]}",
    ];

    [Fact]
    [Trait("criterion", "AC-sdk-validator-3")]
    public void Every_mistake_of_every_rule_is_complete()
    {
        var mistakes = BadDocuments.SelectMany(json => DefinitionValidator.ValidateJson(json, ValidationStage.Version)).ToList();
        var rulesSeen = DefinitionValidator.Rules.Select(rule => rule.Code).Order(StringComparer.Ordinal);

        Assert.All(mistakes, AssertComplete);
        Assert.Equal(
            [MistakeCodes.FormatInvalid, MistakeCodes.FormatVersion, MistakeCodes.DuplicateId],
            mistakes.Select(mistake => mistake.Code).Distinct(StringComparer.Ordinal)
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
