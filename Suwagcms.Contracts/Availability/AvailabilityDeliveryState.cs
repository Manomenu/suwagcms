using System.Text.Json.Serialization;

namespace Suwagcms.Contracts.Availability;

[JsonConverter(typeof(JsonStringEnumConverter<AvailabilityDeliveryState>))]
public enum AvailabilityDeliveryState
{
    Confirmed,
    NotDelivered,
    NotAsked,
}
