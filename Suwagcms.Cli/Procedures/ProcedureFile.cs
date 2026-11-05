using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Suwagcms.Cli.Procedures;

// Why: the file is YAML or JSON and the server takes JSON, so the file becomes one JSON object whose meaning does not depend on the format it was written in.
internal static class ProcedureFile
{
    private static readonly HashSet<string> TopNumbers = new(["revision"], StringComparer.Ordinal);

    private const string OneProcedure = "one procedure: write a mapping with id, description, firstStep and steps";

    public static async Task<(JsonObject? Document, string Problem)> ReadAsync(
        string path,
        CancellationToken cancellationToken,
        string expected = OneProcedure
    )
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
        return Parse(text, path, expected);
    }

    public static (JsonObject? Document, string Problem) Parse(string text, string name, string expected = OneProcedure)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            var node = text.TrimStart().StartsWith('{') ? JsonNode.Parse(text) : FromYaml(text, TopNumbers);
            return node is JsonObject document ? (document, "") : (null, $"'{name}' does not hold {expected}.");
        }
        catch (Exception exception) when (exception is JsonException or YamlException or ArgumentException)
        {
            return (null, $"'{name}' is not valid YAML or JSON: {exception.Message} Fix the syntax and try again.");
        }
    }

    // Why: a file of changes holds procedures too, so it reads the same way and only names its own numbers at the top of each change.
    public static JsonNode? FromYaml(string text, IReadOnlySet<string> topNumbers)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        return stream.Documents.Count == 0 ? null : Convert(stream.Documents[0].RootNode, "", "", topNumbers);
    }

    // Why: YAML writes 42 and true unquoted, and only the bounds of a range, of a local fact or of an action's parameter or result, a timeout and known mean a number or a boolean; every other plain scalar is the text it spells.
    private static JsonNode? Convert(YamlNode node, string key, string parent, IReadOnlySet<string> topNumbers, string grandparent = "")
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                var result = new JsonObject();
                foreach (var (name, value) in mapping.Children)
                {
                    var childKey = (name as YamlScalarNode)?.Value ?? "";
                    result[childKey] = Convert(value, childKey, key, topNumbers, parent);
                }
                return result;
            case YamlSequenceNode sequence:
                return new JsonArray([.. sequence.Children.Select(item => Convert(item, "", key, topNumbers, parent))]);
            case YamlScalarNode scalar:
                return Scalar(scalar, key, parent, topNumbers, grandparent);
            default:
                return null;
        }
    }

    private static JsonNode? Scalar(YamlScalarNode scalar, string key, string parent, IReadOnlySet<string> topNumbers, string grandparent)
    {
        var value = scalar.Value ?? "";
        if (scalar.Style != ScalarStyle.Plain)
        {
            return value;
        }
        if (value is "" or "~" or "null")
        {
            return key == "listen" ? new JsonObject() : null;
        }
        if (key == "known" && parent != "assign" && value is "true" or "false")
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
}
