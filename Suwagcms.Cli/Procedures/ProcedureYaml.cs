using System.Text.Json;
using YamlDotNet.Serialization;

namespace Suwagcms.Cli.Procedures;

internal static class ProcedureYaml
{
    private static readonly ISerializer Serializer = new SerializerBuilder().WithQuotingNecessaryStrings().DisableAliases().Build();

    // Why: the YAML a person edits and the JSON the server sent are one structure, so the text is built from the JSON and never from the records.
    public static string From(JsonElement document) => Serializer.Serialize(Plain(document)).TrimEnd();

    // Why: the server sends a field a procedure does not have as null, and read back from YAML a null block would be an empty one, so nulls are left out.
    private static object? Plain(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => element
                .EnumerateObject()
                .Where(property => property.Value.ValueKind != JsonValueKind.Null)
                .ToDictionary(property => property.Name, property => Plain(property.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(Plain).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetInt32(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Undefined or JsonValueKind.Null => null,
            _ => throw new ArgumentOutOfRangeException(nameof(element)),
        };
}
