using System.Text.Json.Serialization;

namespace Suwagcms.Contracts.Bots;

[JsonConverter(typeof(JsonStringEnumConverter<DeclarationAsk>))]
public enum DeclarationAsk
{
    Answered,
    NotAsked,
    Unreachable,
    Invalid,
}
