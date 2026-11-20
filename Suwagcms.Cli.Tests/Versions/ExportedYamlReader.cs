using System.Globalization;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Suwagcms.Cli.Tests.Versions;

internal static class ExportedYamlReader
{
    public static JsonNode? Read(string text)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(text));
        return Convert(stream.Documents[0].RootNode);
    }

    private static JsonNode? Convert(YamlNode node) =>
        node switch
        {
            YamlMappingNode map => new JsonObject(
                map.Children.Select(child => KeyValuePair.Create(((YamlScalarNode)child.Key).Value ?? "", Convert(child.Value)))
            ),
            YamlSequenceNode list => new JsonArray([.. list.Children.Select(Convert)]),
            YamlScalarNode scalar => Scalar(scalar),
            _ => throw new InvalidOperationException("an exported file holds only mappings, lists and scalars"),
        };

    private static JsonValue? Scalar(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? "";
        if (scalar.Style != ScalarStyle.Plain)
        {
            return JsonValue.Create(value);
        }
        if (value is "" or "~" or "null")
        {
            return null;
        }
        if (value is "true" or "false")
        {
            return JsonValue.Create(value == "true");
        }
        return long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            ? JsonValue.Create(number)
            : JsonValue.Create(value);
    }
}
