using Suwagcms.Contracts.Availability;

namespace Suwagcms.Cli.Availability;

internal sealed record AvailabilityListDocument(string Bot, IReadOnlyList<AvailabilityItem> Resources, AvailabilityDelivery Delivery);
