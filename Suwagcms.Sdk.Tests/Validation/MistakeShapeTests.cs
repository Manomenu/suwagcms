using Suwagcms.Sdk.Format.Articles;
using Suwagcms.Sdk.Tests.Repository;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class MistakeShapeTests
{
    private static readonly string[] BadDocuments =
    [
        "{\"formatVersion\":5,\"knowledgeBases\":[{\"id\":\"Bad Id\",\"title\":\" \",\"embedding\":\"default\",\"results\":5},{\"id\":\"a\",\"title\":\"Offer\",\"language\":\"de\",\"embedding\":\"default\",\"results\":5},{\"id\":\"b\",\"title\":\" offer \",\"embedding\":\"default\",\"results\":5},{\"id\":\"c\",\"title\":\"C\",\"embedding\":\" \",\"results\":21}]}",
        "{\"formatVersion\":5,\"articles\":[{\"id\":\"Bad Id\",\"knowledgeBase\":\"nope\"}]}",
        "{\"formatVersion\":5,\"knowledgeBases\":[{\"id\":\"k\",\"title\":\"T\",\"embedding\":\"default\",\"results\":5}],\"articles\":[{\"id\":\"long\",\"knowledgeBase\":\"k\",\"title\":\"T\",\"content\":\""
            + new string('x', Article.MaxTextLength + 1)
            + "\",\"llmGuide\":\""
            + new string('x', Article.MaxTextLength + 1)
            + "\",\"description\":\"D\"}]}",
        "{\"formatVersion\":5,\"knowledgeBases\":[{\"id\":\"k\",\"title\":\"T\",\"embedding\":\"default\",\"results\":5}],\"articles\":[{\"id\":\"a\",\"knowledgeBase\":\"k\",\"title\":\"T\",\"content\":\"[x](cms://article/none) [y](cms://article/)\",\"llmGuide\":\"G\",\"description\":\"D\"}]}",
        "{\"formatVersion\":5,\"knowledgeBases\":[{\"id\":\"k\",\"title\":\"T\",\"embedding\":\"default\",\"results\":5}],\"articles\":[{\"id\":\"a\",\"knowledgeBase\":\"k\",\"title\":\"T\",\"content\":\"[x](cms://article/b)\",\"llmGuide\":\"G\",\"description\":\"D\"},{\"id\":\"b\",\"knowledgeBase\":\"k\",\"title\":\"T\",\"content\":\"[x](cms://article/a)\",\"llmGuide\":\"G\",\"description\":\"D\"}]}",
        "{\"formatVersion\":5,\"knowledgeBases\":[{\"id\":\"k\",\"title\":\"T\",\"embedding\":\"default\",\"results\":5}],\"articles\":[{\"id\":\"a\",\"knowledgeBase\":\"k\",\"title\":\"T\",\"content\":\"[x](cms://procedure/p)\",\"llmGuide\":\"[x](cms://procedure/conversation)\",\"description\":\"D\"}],\"procedures\":[{\"id\":\"p\",\"description\":\"d\"}]}",
        "",
        "{ not json",
        "[]",
        "{}",
        "{\"formatVersion\":\"x\"}",
        "{\"formatVersion\":4}",
        "{\"formatVersion\":5,\"facts\":[1]}",
        "{\"formatVersion\":5,\"articles\":[{\"id\":\"a\"},{\"id\":\"a\"}]}",
        "{\"formatVersion\":5,\"actions\":[{\"id\":\"Bad Id\",\"description\":\" \",\"parameters\":{\"Bad Name\":{\"type\":\"text\",\"description\":\"d\",\"maxLength\":1}}},{\"id\":\"h\",\"description\":\"d\",\"implementation\":{\"http\":{\"method\":\"GRAB\",\"url\":\"ftp://u:p@x\",\"timeoutSeconds\":99,\"headers\":{\"Authorization\":\"plain\",\"X-Bad\":\"{secret:lower}\",\"Bad Name\":\"x\"}}}},{\"id\":\"u\",\"description\":\"d\",\"implementation\":{\"http\":{\"method\":\"GET\",\"url\":\"https://u:p@x/{none}/{secret:A}?token=1\"}}}]}",
        "{\"formatVersion\":5,\"actions\":[{\"id\":\"a\"},{\"id\":\"a\"}],\"procedures\":[{\"id\":\"p\",\"description\":\"d\"},{\"id\":\"p\",\"description\":\"d\"}],\"knowledgeBases\":[{\"id\":\"k\",\"embedding\":\"default\",\"results\":5},{\"id\":\"k\",\"embedding\":\"default\",\"results\":5}],\"facts\":[{\"name\":\"f\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3},{\"name\":\"f\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3}]}",
        "{\"formatVersion\":5,\"facts\":[{\"name\":\"Bad Name\",\"type\":\"blob\",\"description\":\" \",\"writableBy\":\"nobody\",\"values\":[\"a\"],\"min\":1},{\"name\":\"w\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3,\"values\":[\"a\"]}]}",
        ProcedureSamples.EveryMistake,
        """{"formatVersion":5,"knowledgeBases":[{"id":"faq","title":"F","embedding":"default","results":5},{"id":"offer","title":"O","embedding":"default","results":5}],"articles":[{"id":"a","knowledgeBase":"offer","title":"t"}],"procedures":[{"id":"p","description":"d","firstStep":"t","steps":{"t":{"inform":{"instruction":"i","points":{"a":"b"},"knowledge":[{"knowledgeBase":""},{"knowledgeBase":"faq","articles":["a"]},{"knowledgeBase":"faq"}]}}}}]}""",
        "{\"formatVersion\":5,\"procedures\":[{\"id\":\"conversation\",\"description\":\"d\",\"whenInterrupted\":\"keep\"}]}",
        "{\"formatVersion\":5,\"facts\":[{\"name\":\"e\",\"type\":\"enum\",\"description\":\"d\",\"values\":[\"a\\nb\",\"other\"]},{\"name\":\"a\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3,\"pattern\":{\"means\":\" \",\"example\":\" \",\"tellCustomer\":\"never\"}},{\"name\":\"b\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3,\"pattern\":{\"regex\":\"(?=a)\",\"means\":\"w\"}},{\"name\":\"c\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3,\"pattern\":{\"regex\":\"a*\",\"means\":\"w\"}},{\"name\":\"d\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3,\"pattern\":{\"regex\":\"a\",\"means\":\"w\",\"example\":\"b\"}},{\"name\":\"f\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":1,\"pattern\":{\"regex\":\"ab\",\"means\":\"w\",\"example\":\"ab\"}}]}",
        "{\"formatVersion\":5,\"persona\":{\"personality\":\"" + new string('x', 20_001) + "\"}}",
        "{\"formatVersion\":5,\"facts\":[{\"name\":\"t\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":500},{\"name\":\"e\",\"type\":\"enum\",\"description\":\"d\",\"values\":[\"a\",\"b\"]}],\"actions\":[{\"id\":\"h\",\"description\":\"d\",\"parameters\":{\"p\":{\"type\":\"text\",\"description\":\"d\",\"maxLength\":5},\"q\":{\"type\":\"int\",\"description\":\"d\",\"min\":1,\"max\":2}},\"results\":{\"r\":{\"type\":\"text\",\"description\":\"d\",\"maxLength\":5},\"s\":{\"type\":\"text\",\"description\":\"d\",\"maxLength\":5}},\"implementation\":{\"http\":{\"method\":\"GET\",\"url\":\"https://x.internal\"}}},{\"id\":\"c\",\"description\":\"d\",\"parameters\":{\"p\":{\"type\":\"int\",\"description\":\"d\",\"min\":1,\"max\":2}},\"implementation\":\"code\"}],\"procedures\":[{\"id\":\"a\",\"description\":\"d\",\"steps\":{\"run\":{\"action\":\"h\",\"with\":{\"p\":\"t\",\"none\":\"t\"},\"results\":{\"r\":\"t\",\"s\":\"t\"}},\"typed\":{\"action\":\"h\",\"with\":{\"q\":\"t\"}},\"code\":{\"action\":\"c\",\"with\":{\"p\":\"t\"}},\"said\":{\"say\":\"x\",\"onFailure\":\"run\"}}}]}",
        "{\"formatVersion\":5,\"facts\":[{\"name\":\"p.note\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3}],\"procedures\":[{\"id\":\"p\",\"description\":\"d\"}]}",
        "{\"formatVersion\":5,\"facts\":[{\"name\":\"city\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":3}],\"procedures\":[{\"id\":\"p\",\"description\":\"d\",\"localFacts\":{\"city\":{\"type\":\"text\",\"description\":\"d\",\"maxLength\":3}}}]}",
        "{\"formatVersion\":5,\"facts\":[{\"name\":\"kind\",\"type\":\"enum\",\"description\":\"d\",\"values\":[\"a\",\" \",\"a\",\"a\"]},{\"name\":\"count\",\"type\":\"int\",\"description\":\"d\",\"min\":5},{\"name\":\"range\",\"type\":\"int\",\"description\":\"d\",\"min\":5,\"max\":1},{\"name\":\"single\",\"type\":\"enum\",\"description\":\"d\",\"values\":[\"a\"]},{\"name\":\"note\",\"type\":\"text\",\"description\":\"d\",\"maxLength\":0}]}",
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

    [Fact]
    [Trait("criterion", "AC-procedure-creator-47")]
    public void No_mistake_of_any_rule_speaks_of_a_rule_or_of_when_to_an_author()
    {
        var mistakes = BadDocuments.SelectMany(json => DefinitionValidator.ValidateJson(json, ValidationStage.Version));

        Assert.All(mistakes, mistake => Assert.DoesNotMatch("(?i)'when'|\\brules?\\b", $"{mistake.Message} {mistake.Fix}"));
    }

    [Fact]
    [Trait("criterion", "AC-procedure-creator-47")]
    public void No_text_of_the_sdk_puts_when_in_quotes()
    {
        var quoting = Directory
            .EnumerateFiles(RepositoryFiles.Path("Suwagcms.Sdk"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains("/obj/", StringComparison.Ordinal) && !file.Contains("/bin/", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains("'when'", StringComparison.Ordinal));

        Assert.Empty(quoting);
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
