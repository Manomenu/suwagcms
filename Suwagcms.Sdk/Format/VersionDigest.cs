using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Suwagcms.Sdk.Format;

public static class VersionDigest
{
    public static string Of(JsonElement document)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            Write(writer, document, isRoot: true);
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
    }

    public static string Of(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        return Of(document.RootElement);
    }

    private static void Write(Utf8JsonWriter writer, JsonElement element, bool isRoot)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (
                    var member in element
                        .EnumerateObject()
                        .Where(member => !(isRoot && member.NameEquals("digest")))
                        .OrderBy(member => member.Name, StringComparer.Ordinal)
                )
                {
                    writer.WritePropertyName(member.Name);
                    Write(writer, member.Value, isRoot: false);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    Write(writer, item, isRoot: false);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText());
                break;
            case JsonValueKind.Undefined:
            case JsonValueKind.String:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
