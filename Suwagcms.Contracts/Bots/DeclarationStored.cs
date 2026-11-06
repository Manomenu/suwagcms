using System.Text.Json.Serialization;

namespace Suwagcms.Contracts.Bots;

/// <summary>What the CMS holds of a bot's declaration: no good answer yet, the answer that the bot declares nothing, or a declaration.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DeclarationStored>))]
public enum DeclarationStored
{
    NeverAnswered,
    DeclaresNothing,
    Declared,
}
