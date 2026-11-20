using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.Articles;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.KnowledgeBases;
using Suwagcms.Sdk.Format.Personas;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Server.Editing;

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
            ResourceKind.Action => typeof(BotAction),
            ResourceKind.Persona => typeof(Persona),
            ResourceKind.Definition => throw new InvalidOperationException(
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
            ResourceKind.Definition => "definition",
            ResourceKind.Action => "action",
            ResourceKind.Persona => "persona",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    public static string Word(ResourceKind kind) =>
        kind switch
        {
            ResourceKind.GlobalFact => "fact",
            ResourceKind.KnowledgeBase => "knowledgeBase",
            ResourceKind.Article => "article",
            ResourceKind.Procedure => "procedure",
            ResourceKind.Action => "action",
            ResourceKind.Persona => "persona",
            ResourceKind.Definition => throw new InvalidOperationException(
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
            Actions = [.. all.Where(item => item.Target.Kind == ResourceKind.Action).Select(item => (BotAction)item.Record)],
            Procedures = [.. all.Where(item => item.Target.Kind == ResourceKind.Procedure).Select(item => (Procedure)item.Record)],
            Persona = PersonaOf(all),
        };
    }

    public static Persona? PersonaOf(IEnumerable<StoredResource> resources) =>
        resources
            .Where(item => item.Target.Kind == ResourceKind.Persona)
            .Select(item => (Persona)item.Record)
            .FirstOrDefault(persona => persona.HasText());

    public static object WithId(object record, string id) =>
        record switch
        {
            GlobalFact fact => fact with { Name = id },
            KnowledgeBase knowledgeBase => knowledgeBase with { Id = id },
            Article article => article with { Id = id },
            Procedure procedure => procedure with { Id = id },
            BotAction action => action with { Id = id },
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

    public static bool Same(object record, object other) =>
        JsonNode.DeepEquals(JsonNode.Parse(Write(record)), JsonNode.Parse(Write(other)));

    public static object Read(ResourceKind kind, string body) =>
        JsonSerializer.Deserialize(body, RecordType(kind), Json) ?? throw new InvalidOperationException($"A stored {kind} body is null.");
}
