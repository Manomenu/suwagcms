using System.Text.Json;
using Suwagcms.Sdk.BotRuntime;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.Validation;

public sealed class MistakeCodeTests
{
    private static readonly JsonSerializerOptions[] Readers = [JsonSerializerOptions.Default, BotRuntimeJson.Options];

    [Fact]
    [Trait("criterion", "AC-sdk-validator-20")]
    public void Every_code_is_sent_as_its_own_dotted_text()
    {
        var texts = Enum.GetValues<MistakeCode>().Select(code => code.Text()).ToList();

        Assert.All(texts, text => Assert.Matches("^[a-z][A-Za-z]*(\\.[a-z][A-Za-z]*)+$", text));
        Assert.Equal(texts.Count, texts.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    [Trait("criterion", "AC-sdk-validator-20")]
    public void Every_code_reads_back_as_itself_wherever_the_sdk_reads_json()
    {
        foreach (var options in Readers)
        {
            Assert.All(
                Enum.GetValues<MistakeCode>(),
                code =>
                {
                    var sent = JsonSerializer.Serialize(code, options);
                    Assert.Equal($"\"{code.Text()}\"", sent);
                    Assert.Equal(code, JsonSerializer.Deserialize<MistakeCode>(sent, options));
                }
            );
        }
    }
}
