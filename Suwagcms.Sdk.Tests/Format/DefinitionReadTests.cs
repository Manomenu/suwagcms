using System.Text.Json;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Tests.Format;

public sealed class DefinitionReadTests
{
    [Fact]
    [Trait("criterion", "AC-bot-runtime-sdk-19")]
    public void A_bot_reads_a_definition_with_the_options_of_the_format_so_a_newer_optional_field_is_skipped()
    {
        var definition = BotDefinition.Read(
            """{"formatVersion":2,"fromTheFuture":{"a":1},"facts":[{"name":"a","type":"text","description":"d","maxLength":3,"new":1}]}"""
        );

        Assert.Equal(("a", 3), (definition.Facts[0].Name, definition.Facts[0].MaxLength));
    }

    [Theory]
    [Trait("criterion", "AC-bot-runtime-sdk-19")]
    [InlineData("""{"formatVersion":2,"facts":[{"name":"a","type":"text","description":"d","maxLength":"3"}]}""")]
    [InlineData("""{"formatVersion":2,"Facts":[],"facts":null}""")]
    [InlineData("""{"facts":[]}""")]
    [InlineData("{ not json")]
    public void A_document_that_does_not_fit_the_format_throws_as_the_format_says(string json) =>
        Assert.Throws<JsonException>(() => BotDefinition.Read(json));

    [Fact]
    [Trait("criterion", "AC-bot-runtime-sdk-19")]
    public void A_null_document_is_no_definition() => Assert.Throws<InvalidDataException>(() => BotDefinition.Read("null"));
}
