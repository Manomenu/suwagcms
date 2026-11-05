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
            ResourceKind.Article => typeof(Article),
            ResourceKind.Procedure => typeof(Procedure),
            ResourceKind.Definition or ResourceKind.Action => throw new InvalidOperationException(
                $"{kind} resources are not stored yet: add the kind to ResourceKinds."
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    public static string Noun(ResourceKind kind) =>
        kind switch
        {
            ResourceKind.GlobalFact => "fact",
            ResourceKind.KnowledgeBase => "knowledge base",
            ResourceKind.Article => "article",
            ResourceKind.Procedure => "procedure",
            ResourceKind.Definition or ResourceKind.Action => throw new InvalidOperationException(
                $"{kind} resources are not stored yet: add the kind to ResourceKinds."
            ),
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
            Articles = [.. all.Where(item => item.Target.Kind == ResourceKind.Article).Select(item => (Article)item.Record)],
            Procedures = [.. all.Where(item => item.Target.Kind == ResourceKind.Procedure).Select(item => (Procedure)item.Record)],
        };
    }

    public static object WithId(object record, string id) =>
        record switch
        {
            GlobalFact fact => fact with { Name = id },
            KnowledgeBase knowledgeBase => knowledgeBase with { Id = id },
            Article article => article with { Id = id },
            Procedure procedure => procedure with { Id = id },
            _ => throw new ArgumentException($"{record.GetType().Name} is not a stored resource."),
        };

    public static object Rewritten(object record, ResourceRef target, string newId) =>
        record switch
        {
            Article article => ReferenceWalker.Rewrite(article, target, newId),
            Procedure procedure => ReferenceWalker.Rewrite(procedure, target, newId),
            _ => record,
        };

    public static string Column(ResourceKind kind) => JsonNamingPolicy.CamelCase.ConvertName(kind.ToString());

    public static ResourceKind FromColumn(string column) => Enum.Parse<ResourceKind>(column, ignoreCase: true);

    public static string Write(object record) => JsonSerializer.Serialize(record, Json);

    public static object Read(ResourceKind kind, string body) =>
        JsonSerializer.Deserialize(body, RecordType(kind), Json) ?? throw new InvalidOperationException($"A stored {kind} body is null.");
}
