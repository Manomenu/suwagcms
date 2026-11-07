using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Cli.Yaml;
using YamlDotNet.Core;

namespace Suwagcms.Cli.Validation;

internal static class DefinitionFile
{
    private static readonly HashSet<string> TopNumbers = new(
        ["formatVersion", "number", "min", "max", "maxLength", "results", "namedArticlesLimit"],
        StringComparer.Ordinal
    );

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
            return FormatYaml.Parse(text, TopNumbers) is JsonObject document
                ? (JsonSerializer.SerializeToElement(document), "", false)
                : (null, $"'{path}' does not hold one mapping: write the fields of the format at the top level.", false);
        }
        catch (Exception exception) when (exception is JsonException or YamlException or ArgumentException)
        {
            return (null, $"'{path}' is not valid YAML or JSON: {exception.Message} Fix the syntax and try again.", false);
        }
    }
}
