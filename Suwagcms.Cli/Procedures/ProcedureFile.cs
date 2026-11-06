using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Cli.Yaml;
using YamlDotNet.Core;

namespace Suwagcms.Cli.Procedures;

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
            return FormatYaml.Parse(text, TopNumbers) is JsonObject document
                ? (document, "")
                : (null, $"'{name}' does not hold {expected}.");
        }
        catch (Exception exception) when (exception is JsonException or YamlException or ArgumentException)
        {
            return (null, $"'{name}' is not valid YAML or JSON: {exception.Message} Fix the syntax and try again.");
        }
    }
}
