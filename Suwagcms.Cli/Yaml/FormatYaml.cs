using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Suwagcms.Cli.Yaml;

// Why: every file the CLI reads is YAML or JSON and the server takes JSON, so all of them become one JSON document by one rule, whose meaning does not depend on the command reading it or the format it was written in.
internal static class FormatYaml
{
    // Why: only the top of a file differs from command to command; a list's items count as a top, since a file of changes or a definition lists whole resources.
    public static JsonNode? Parse(string text, IReadOnlySet<string> topNumbers)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.TrimStart().StartsWith('{'))
        {
            return JsonNode.Parse(text);
        }
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        return stream.Documents.Count == 0 ? null : Convert(stream.Documents[0].RootNode, "", "", "", topNumbers);
    }

    private static JsonNode? Convert(YamlNode node, string key, string parent, string grandparent, IReadOnlySet<string> topNumbers)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                var result = new JsonObject();
                foreach (var (name, value) in mapping.Children)
                {
                    var childKey = (name as YamlScalarNode)?.Value ?? "";
                    result[childKey] = Convert(value, childKey, key, parent, topNumbers);
                }
                return result;
            case YamlSequenceNode sequence:
                return new JsonArray([.. sequence.Children.Select(item => Convert(item, "", key, parent, topNumbers))]);
            case YamlScalarNode scalar:
                return Scalar(scalar, key, parent, grandparent, topNumbers);
            default:
                return null;
        }
    }

    // Why: YAML writes 42 and true unquoted, and only the bounds of a range, of a local fact or of an action's parameter or result, a timeout, known and what the top of a file names mean a number or a boolean; every other plain scalar is the text it spells.
    private static JsonNode? Scalar(YamlScalarNode scalar, string key, string parent, string grandparent, IReadOnlySet<string> topNumbers)
    {
        var value = scalar.Value ?? "";
        if (scalar.Style != ScalarStyle.Plain)
        {
            return value;
        }
        if (value is "" or "~" or "null")
        {
            return key switch
            {
                "listen" => new JsonObject(),
                // Why: a block left empty is work still to do, which a save lists; a null would make it no block at all.
                "start" or "instruction" => "",
                _ => parent == "points" ? "" : null,
            };
        }
        var isBoolean = (key == "known" && parent is not ("assign" or "points")) || (key == "declaresNothing" && parent.Length == 0);
        if (isBoolean && value is "true" or "false")
        {
            return value == "true";
        }
        var isNumber =
            (key is "min" or "max" && parent == "range")
            || (key is "min" or "max" or "maxLength" && grandparent is "localFacts" or "parameters" or "results")
            || (key == "timeoutSeconds" && parent == "http")
            || (parent.Length == 0 && topNumbers.Contains(key));
        return isNumber && int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            ? number
            : value;
    }

    // Why: the YAML a person reads and the JSON the server sent are one structure, so the text is built from the JSON and never from the records.
    public static object? Plain(JsonElement element, bool keepNulls) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => element
                .EnumerateObject()
                .Where(property => keepNulls || property.Value.ValueKind != JsonValueKind.Null)
                .ToDictionary(property => property.Name, property => Plain(property.Value, keepNulls)),
            JsonValueKind.Array => element.EnumerateArray().Select(item => Plain(item, keepNulls)).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetInt64(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Undefined or JsonValueKind.Null => null,
            _ => throw new ArgumentOutOfRangeException(nameof(element)),
        };
}
