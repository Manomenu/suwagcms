using System.Text.Json.Serialization;

namespace Suwagcms.Contracts.Bots;

[JsonConverter(typeof(JsonStringEnumConverter<DeclarationStored>))]
public enum DeclarationStored
{
    NeverAnswered,
    DeclaresNothing,
    Declared,
}
