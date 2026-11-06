using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Cli.Yaml;
using YamlDotNet.Core;

namespace Suwagcms.Cli.Changes;

// Why: the YAML is only the CLI's form of the request, so the file becomes the JSON the server takes and every rule about its contents stays on the server.
internal static class ChangeFile
{
    private static readonly HashSet<string> TopNumbers = new(["ifRevision", "min", "max", "maxLength"], StringComparer.Ordinal);

    public static async Task<(JsonObject? Document, string Problem)> ReadAsync(string path, CancellationToken cancellationToken)
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return (null, $"Cannot read '{path}': {exception.Message} Give the path of a YAML or JSON file of changes.");
        }
        return Parse(text, path);
    }

    private static (JsonObject? Document, string Problem) Parse(string text, string name)
    {
        JsonNode? node;
        try
        {
            node = FormatYaml.Parse(text, TopNumbers);
        }
        catch (YamlException exception)
        {
            return (
                null,
                $"'{name}' is not valid YAML, at line {Number(exception.Start.Line)}: {exception.Message} Fix the syntax and try again."
            );
        }
        catch (JsonException exception)
        {
            return (
                null,
                $"'{name}' is not valid JSON, at line {Number((exception.LineNumber ?? 0) + 1)}: {exception.Message} Fix the syntax and try again."
            );
        }
        if (
            node is not JsonObject document
            || !document.TryGetPropertyValue("changes", out var changes)
            || changes is not (JsonArray or null)
        )
        {
            return (
                null,
                $"'{name}' has no `changes` list: write `changes:` with a list of `add: <kind>`, `edit: <kind>` and `delete: <kind>`."
            );
        }
        // Why: `changes:` with nothing under it reads as null, and it says the same as an empty list.
        document["changes"] ??= new JsonArray();
        return (document, "");
    }

    private static string Number(long number) => number.ToString(CultureInfo.InvariantCulture);
}
