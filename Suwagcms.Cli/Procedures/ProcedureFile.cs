using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Suwagcms.Cli.Procedures;

// Why: the file is YAML or JSON and the server takes JSON, so the file becomes one JSON object whose meaning does not depend on the format it was written in.
internal static class ProcedureFile
{
    public static async Task<(JsonObject? Document, string Problem)> ReadAsync(string path, CancellationToken cancellationToken)
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, $"Cannot read '{path}': {exception.Message} Pass --file with the path of a YAML or JSON file.");
        }
        return Parse(text, path);
    }

    public static (JsonObject? Document, string Problem) Parse(string text, string name)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            var node = text.TrimStart().StartsWith('{') ? JsonNode.Parse(text) : FromYaml(text);
            return node is JsonObject document
                ? (document, "")
                : (null, $"'{name}' does not hold one procedure: write a mapping with id, description, firstStep and steps.");
        }
        catch (Exception exception) when (exception is JsonException or YamlException or ArgumentException)
        {
            return (null, $"'{name}' is not valid YAML or JSON: {exception.Message} Fix the syntax and try again.");
        }
    }

    private static JsonNode? FromYaml(string text)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        return stream.Documents.Count == 0 ? null : Convert(stream.Documents[0].RootNode, "", "");
    }

    // Why: YAML writes 42 and true unquoted, and only min, max and known mean a number or a boolean; every other plain scalar is the text it spells.
    private static JsonNode? Convert(YamlNode node, string key, string parent)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                var result = new JsonObject();
                foreach (var (name, value) in mapping.Children)
                {
                    var childKey = (name as YamlScalarNode)?.Value ?? "";
                    result[childKey] = Convert(value, childKey, key);
                }
                return result;
            case YamlSequenceNode sequence:
                return new JsonArray([.. sequence.Children.Select(item => Convert(item, "", key))]);
            case YamlScalarNode scalar:
                return Scalar(scalar, key, parent);
            default:
                return null;
        }
    }

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
                // Why: a block left empty is work still to do, which a save lists; a null would make it no block at all.
                "start" => "",
                _ => null,
            };
        }
        if (key == "known" && parent != "assign" && value is "true" or "false")
        {
            return value == "true";
        }
        var isNumber = (key is "min" or "max" && parent == "range") || (key == "revision" && parent.Length == 0);
        return isNumber && int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            ? number
            : value;
    }
}
