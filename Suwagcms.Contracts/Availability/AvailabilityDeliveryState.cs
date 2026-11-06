using System.Text.Json.Serialization;

namespace Suwagcms.Contracts.Availability;

/// <summary>What the bot's own answer says about the setting: <c>Confirmed</c> it reports the CMS's number, <c>NotDelivered</c> it does not or could not be asked, <c>NotAsked</c> nobody asked it, as with nothing ever turned off or a dry run.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AvailabilityDeliveryState>))]
public enum AvailabilityDeliveryState
{
    Confirmed,
    NotDelivered,
    NotAsked,
}
