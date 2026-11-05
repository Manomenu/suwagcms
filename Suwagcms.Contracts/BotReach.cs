using System.Text.Json.Serialization;

namespace Suwagcms.Contracts;

/// <summary>What asking a bot for its state came to: it answered, it could not be reached, or the address answers as another bot.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<BotReach>))]
public enum BotReach
{
    Answered,
    Unreachable,
    WrongAddress,
}
