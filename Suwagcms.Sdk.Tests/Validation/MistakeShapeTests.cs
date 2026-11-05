using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class MistakeShapeTests
{
    private static readonly string[] BadDocuments =
    [
        "{\"formatVersion\":1,\"knowledgeBases\":[{\"id\":\"Bad Id\",\"title\":\" \"},{\"id\":\"a\",\"title\":\"Offer\",\"language\":\"de\"},{\"id\":\"b\",\"title\":\" offer \"}]}",
        "{\"formatVersion\":1,\"articles\":[{\"id\":\"Bad Id\",\"knowledgeBase\":\"nope\"}]}",
        "{\"formatVersion\":1,\"knowledgeBases\":[{\"id\":\"k\",\"title\":\"T\"}],\"articles\":[{\"id\":\"long\",\"knowledgeBase\":\"k\",\"title\":\"T\",\"content\":\""
            + new string('x', Article.MaxTextLength + 1)
            + "\",\"llmGuide\":\""
            + new string('x', Article.MaxTextLength + 1)
            + "\",\"description\":\"D\"}]}",
        "{\"formatVersion\":1,\"knowledgeBases\":[{\"id\":\"k\",\"title\":\"T\"}],\"articles\":[{\"id\":\"a\",\"knowledgeBase\":\"k\",\"title\":\"T\",\"content\":\"[x](cms://article/none) [y](cms://article/)\",\"llmGuide\":\"G\",\"description\":\"D\"}]}",
        "{\"formatVersion\":1,\"knowledgeBases\":[{\"id\":\"k\",\"title\":\"T\"}],\"articles\":[{\"id\":\"a\",\"knowledgeBase\":\"k\",\"title\":\"T\",\"content\":\"[x](cms://article/b)\",\"llmGuide\":\"G\",\"description\":\"D\"},{\"id\":\"b\",\"knowledgeBase\":\"k\",\"title\":\"T\",\"content\":\"[x](cms://article/a)\",\"llmGuide\":\"G\",\"description\":\"D\"}]}",
        "",
        "{ not json",
        "[]",
        "{}",
        "{\"formatVersion\":\"x\"}",
        "{\"formatVersion\":3}",
        "{\"formatVersion\":1,\"facts\":[1]}",
        "{\"formatVersion\":1,\"articles\":[{\"id\":\"a\"},{\"id\":\"a\"}]}",
        "{\"formatVersion\":1,\"actions\":[{\"id\":\"Bad Id\",\"description\":\" \",\"parameters\":{\"Bad Name\":{\"type\":\"text\",\"description\":\"d\",\"maxLength\":1}}},{\"id\":\"h\",\"description\":\"d\",\"implementation\":{\"http\":{\"method\":\"GRAB\",\"url\":\"ftp://u:p@x\",\"timeoutSeconds\":99,\"headers\":{\"Authorization\":\"plain\",\"X-Bad\":\"{secret:lower}\",\"Bad Name\":\"x\"}}}},{\"id\":\"u\",\"description\":\"d\",\"implementation\":{\"http\":{\"method\":\"GET\",\"url\":\"https://u:p@x/{none}/{secret:A}?token=1\"}}}]}",
        "{\"formatVersion\":1,\"actions\":[{\"id\":\"a\"},{\"id\":\"a\"}],\"procedures\":[{\"id\":\"p\",\"description\":\"d\"},{\"id\":\"p\",\"description\":\"d\"}],\"knowledgeBases\":[{\"id\":\"k\"},{\"id\":\"k\"}],\"facts\":[{\"name\":\"f\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3},{\"name\":\"f\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3}]}",
        "{\"formatVersion\":1,\"facts\":[{\"name\":\"Bad Name\",\"type\":\"blob\",\"description\":\" \",\"writableBy\":\"nobody\",\"values\":[\"a\"],\"min\":1},{\"name\":\"w\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3,\"values\":[\"a\"]}]}",
        ProcedureSamples.EveryMistake,
        "{\"formatVersion\":1,\"facts\":[{\"name\":\"t\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":500},{\"name\":\"e\",\"type\":\"enum\",\"description\":\"d\",\"values\":[\"a\",\"b\"]}],\"actions\":[{\"id\":\"h\",\"description\":\"d\",\"parameters\":{\"p\":{\"type\":\"text\",\"description\":\"d\",\"maxLength\":5},\"q\":{\"type\":\"int\",\"description\":\"d\",\"min\":1,\"max\":2}},\"results\":{\"r\":{\"type\":\"text\",\"description\":\"d\",\"maxLength\":5},\"s\":{\"type\":\"text\",\"description\":\"d\",\"maxLength\":5}},\"implementation\":{\"http\":{\"method\":\"GET\",\"url\":\"https://x.internal\"}}},{\"id\":\"c\",\"description\":\"d\",\"parameters\":{\"p\":{\"type\":\"int\",\"description\":\"d\",\"min\":1,\"max\":2}},\"implementation\":\"code\"}],\"procedures\":[{\"id\":\"a\",\"description\":\"d\",\"steps\":{\"run\":{\"action\":\"h\",\"with\":{\"p\":\"t\",\"none\":\"t\"},\"results\":{\"r\":\"t\",\"s\":\"t\"}},\"typed\":{\"action\":\"h\",\"with\":{\"q\":\"t\"}},\"code\":{\"action\":\"c\",\"with\":{\"p\":\"t\"}},\"said\":{\"say\":\"x\",\"onFailure\":\"run\"}}}]}",
        "{\"formatVersion\":1,\"facts\":[{\"name\":\"p.note\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3}],\"procedures\":[{\"id\":\"p\",\"description\":\"d\"}]}",
        "{\"formatVersion\":1,\"facts\":[{\"name\":\"city\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3}],\"procedures\":[{\"id\":\"p\",\"description\":\"d\",\"localFacts\":{\"city\":{\"type\":\"text\",\"description\":\"d\",\"maxLength\":3}}}]}",
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
