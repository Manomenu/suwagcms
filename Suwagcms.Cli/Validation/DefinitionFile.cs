using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Suwagcms.Cli.Validation;

// Why: the validator reads JSON and an author may write YAML, so the file becomes one JSON document whose meaning does not depend on the format it was written in.
internal static class DefinitionFile
{
    private static readonly string[] Numbers = ["formatVersion", "number", "min", "max", "maxLength", "timeoutSeconds"];
    private static readonly string[] Booleans = ["known", "declaresNothing"];

    // Why: YAML writes 42 and true unquoted, and a text may spell either, so only the fields of the format that hold a number or a boolean read one.
    public static JsonNode? Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.TrimStart().StartsWith('{'))
        {
            return JsonNode.Parse(text);
        }
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        return stream.Documents.Count == 0 ? null : Convert(stream.Documents[0].RootNode, "", "");
    }

    public static async Task<(JsonElement? Document, string Problem, bool Unreadable)> ReadAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, $"Cannot read '{path}': {exception.Message} Pass the path of a YAML or JSON file.", true);
        }
        try
        {
            return Parse(text) is JsonObject document
                ? (JsonSerializer.SerializeToElement(document), "", false)
                : (null, $"'{path}' does not hold one mapping: write the fields of the format at the top level.", false);
        }
        catch (Exception exception) when (exception is JsonException or YamlException or ArgumentException)
        {
            return (null, $"'{path}' is not valid YAML or JSON: {exception.Message} Fix the syntax and try again.", false);
        }
    }

    private static JsonNode? Convert(YamlNode node, string key, string parent) =>
        node switch
        {
            YamlMappingNode mapping => new JsonObject(
                mapping.Children.Select(child =>
                {
                    var name = (child.Key as YamlScalarNode)?.Value ?? "";
                    return KeyValuePair.Create(name, Convert(child.Value, name, key));
                })
            ),
            YamlSequenceNode sequence => new JsonArray([.. sequence.Children.Select(item => Convert(item, "", key))]),
            YamlScalarNode scalar => Scalar(scalar, key, parent),
            _ => null,
        };

    private static JsonNode? Scalar(YamlScalarNode scalar, string key, string parent)
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
                // Why: a block left empty is work still to do, which the Version stage lists; a null would make it no block at all.
                "start" or "instruction" => "",
                _ => parent == "points" ? "" : null,
            };
        }
        if (parent is not ("assign" or "points") && Booleans.Contains(key, StringComparer.Ordinal) && value is "true" or "false")
        {
            return value == "true";
        }
        return
            parent is not ("assign" or "points")
            && Numbers.Contains(key, StringComparer.Ordinal)
            && int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            ? number
            : value;
    }
}
