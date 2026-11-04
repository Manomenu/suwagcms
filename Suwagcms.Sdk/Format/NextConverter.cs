using System.Text.Json;
using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format;

internal sealed class NextConverter : JsonConverter<NextSteps>
{
    public override NextSteps Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new NextSteps { Step = reader.GetString() };
        }
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            return new NextSteps { Rules = JsonSerializer.Deserialize<List<Transition>>(ref reader, options) ?? [] };
        }
        throw new JsonException("next is a step's id, or a list of rules { when, to }.");
    }

    public override void Write(Utf8JsonWriter writer, NextSteps value, JsonSerializerOptions options)
    {
        if (value.Step is not null)
        {
            writer.WriteStringValue(value.Step);
        }
        else
        {
            JsonSerializer.Serialize(writer, value.Rules ?? [], options);
        }
    }
}
