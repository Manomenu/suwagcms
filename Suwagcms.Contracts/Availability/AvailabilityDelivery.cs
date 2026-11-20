namespace Suwagcms.Contracts.Availability;

public sealed record AvailabilityDelivery(AvailabilityDeliveryState State, int Number, int? BotNumber, string? Reason);
