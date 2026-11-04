using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Server.Editing;

// Why: the one place that knows which kinds are stored and where each lands in a definition; a new kind is a line in each method.
internal static class ResourceKinds
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static Type RecordType(ResourceKind kind) =>
        kind switch
        {
            ResourceKind.GlobalFact => typeof(GlobalFact),
            ResourceKind.KnowledgeBase => typeof(KnowledgeBase),
            ResourceKind.Definition or ResourceKind.Article or ResourceKind.Action or ResourceKind.Procedure =>
                throw new InvalidOperationException($"{kind} resources are not stored yet: add the kind to ResourceKinds."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    public static string Noun(ResourceKind kind) =>
        kind switch
        {
            ResourceKind.GlobalFact => "fact",
            ResourceKind.KnowledgeBase => "knowledge base",
            ResourceKind.Definition or ResourceKind.Article or ResourceKind.Action or ResourceKind.Procedure =>
                throw new InvalidOperationException($"{kind} resources are not stored yet: add the kind to ResourceKinds."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    public static BotDefinition Assemble(IEnumerable<StoredResource> resources)
    {
        var all = resources.ToList();
        return new BotDefinition
        {
            FormatVersion = BotDefinition.CurrentFormatVersion,
            Facts = [.. all.Where(item => item.Target.Kind == ResourceKind.GlobalFact).Select(item => (GlobalFact)item.Record)],
            KnowledgeBases =
            [
                .. all.Where(item => item.Target.Kind == ResourceKind.KnowledgeBase).Select(item => (KnowledgeBase)item.Record),
            ],
        };
    }

    public static string Column(ResourceKind kind) => JsonNamingPolicy.CamelCase.ConvertName(kind.ToString());

    public static ResourceKind FromColumn(string column) => Enum.Parse<ResourceKind>(column, ignoreCase: true);

    public static string Write(object record) => JsonSerializer.Serialize(record, Json);

    public static object Read(ResourceKind kind, string body) =>
        JsonSerializer.Deserialize(body, RecordType(kind), Json) ?? throw new InvalidOperationException($"A stored {kind} body is null.");
}
