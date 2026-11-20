using System.Text.Json.Serialization;

namespace Suwagcms.Contracts.Bots;

[JsonConverter(typeof(JsonStringEnumConverter<BotReach>))]
public enum BotReach
{
    Answered,
    Unreachable,
    WrongAddress,
}
