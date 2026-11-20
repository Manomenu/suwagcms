namespace Suwagcms.Sdk.Format.References;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ResourceKind
{
    Definition,
    GlobalFact,
    KnowledgeBase,
    Article,
    Action,
    Procedure,
    Persona,
}
