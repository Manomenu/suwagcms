namespace Suwagcms.Sdk.Format;

// Why: a mistake is read back as text by every client, so the enum names itself instead of relying on one host's serializer settings.
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ResourceKind
{
    Definition,
    GlobalFact,
    KnowledgeBase,
    Article,
    Action,
    Procedure,
}
