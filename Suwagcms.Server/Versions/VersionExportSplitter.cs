using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Server.Versions;

// Why: the files come only from the frozen document and carry no date, so exporting a version twice gives the same files.
internal static class VersionExportSplitter
{
    private static readonly (string List, ResourceKind Kind, string Folder, string Key)[] Kinds =
    [
        ("facts", ResourceKind.GlobalFact, "facts", "name"),
        ("knowledgeBases", ResourceKind.KnowledgeBase, "knowledge-bases", "id"),
        ("articles", ResourceKind.Article, "articles", "id"),
        ("procedures", ResourceKind.Procedure, "procedures", "id"),
    ];

    private static readonly string[] Envelope = ["bot", "number", "formatVersion", "digest"];

    public static VersionExport Split(string bot, int number, string document)
    {
        var root = JsonNode.Parse(document) as JsonObject ?? throw new InvalidOperationException("A stored version is not a JSON object.");
        var files = new List<ExportedFile> { new(null, "version", "version.yaml", Element(WithEnvelopeFirst(root))) };
        foreach (var kind in Kinds)
        {
            foreach (var resource in (root[kind.List] as JsonArray ?? []).OfType<JsonObject>())
            {
                var id = resource[kind.Key]?.GetValue<string>() ?? throw new InvalidOperationException($"A resource has no {kind.Key}.");
                files.Add(new ExportedFile(kind.Kind, id, $"{kind.Folder}/{id}.yaml", Element(resource)));
            }
        }
        return new VersionExport(bot, number, files);
    }

    // Why: a list the export has no folder for yet stays in version.yaml, so a version's content is never lost from its export.
    private static JsonObject WithEnvelopeFirst(JsonObject root)
    {
        var inFolders = Kinds.Select(kind => kind.List).ToHashSet(StringComparer.Ordinal);
        var head = new JsonObject();
        foreach (var name in Envelope)
        {
            head[name] = root[name]?.DeepClone();
        }
        foreach (var (name, value) in root)
        {
            if (!inFolders.Contains(name) && !Envelope.Contains(name, StringComparer.Ordinal) && value is not JsonArray { Count: 0 })
            {
                head[name] = value?.DeepClone();
            }
        }
        return head;
    }

    private static JsonElement Element(JsonObject value) => JsonSerializer.SerializeToElement(value);
}
