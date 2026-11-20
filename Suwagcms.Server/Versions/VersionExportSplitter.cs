using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.Format.Personas;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Server.Versions;

internal static class VersionExportSplitter
{
    private static readonly (string List, ResourceKind Kind, string Folder, string Key)[] Kinds =
    [
        ("facts", ResourceKind.GlobalFact, "facts", "name"),
        ("knowledgeBases", ResourceKind.KnowledgeBase, "knowledge-bases", "id"),
        ("articles", ResourceKind.Article, "articles", "id"),
        ("actions", ResourceKind.Action, "actions", "id"),
        ("procedures", ResourceKind.Procedure, "procedures", "id"),
    ];

    private const string PersonaMember = "persona";

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
        if (root[PersonaMember] is JsonObject persona)
        {
            files.Add(new ExportedFile(ResourceKind.Persona, Persona.PublicId, $"{PersonaMember}.yaml", Element(persona)));
        }
        return new VersionExport(bot, number, files);
    }

    private static JsonObject WithEnvelopeFirst(JsonObject root)
    {
        var inFolders = Kinds.Select(kind => kind.List).Append(PersonaMember).ToHashSet(StringComparer.Ordinal);
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
