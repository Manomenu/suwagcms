using System.Text.Json.Serialization;

namespace Suwagcms.Contracts;

/// <summary>What this look at a bot's declaration came to: the bot gave a good answer, was not asked (the stored one was read), could not be reached, or gave an answer that was refused.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DeclarationAsk>))]
public enum DeclarationAsk
{
    Answered,
    NotAsked,
    Unreachable,
    Invalid,
}
