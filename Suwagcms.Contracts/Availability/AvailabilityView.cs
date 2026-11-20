namespace Suwagcms.Contracts.Availability;

public sealed record AvailabilityView(int Number, IReadOnlyList<AvailabilityItem> Resources, AvailabilityDelivery Delivery);
