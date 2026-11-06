using System.Text.Json;
using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format.Actions;

internal sealed class ActionImplementationConverter : JsonConverter<ActionImplementation>
{
    private const string Shape = "implementation is the word code, or { http: { method, url, headers, timeoutSeconds } }.";

    private sealed record Wire
    {
        public HttpCall? Http { get; init; }
    }

    public override ActionImplementation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && reader.GetString() == ActionImplementation.CodeWord)
        {
            return new ActionImplementation { IsCode = true };
        }
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return new ActionImplementation { Http = JsonSerializer.Deserialize<Wire>(ref reader, options)?.Http };
        }
        throw new JsonException(Shape);
    }

    public override void Write(Utf8JsonWriter writer, ActionImplementation value, JsonSerializerOptions options)
    {
        if (value.IsCode)
        {
            writer.WriteStringValue(ActionImplementation.CodeWord);
        }
        else
        {
            JsonSerializer.Serialize(writer, new Wire { Http = value.Http }, options);
        }
    }
}
