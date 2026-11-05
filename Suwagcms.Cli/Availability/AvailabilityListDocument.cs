using Suwagcms.Contracts;

namespace Suwagcms.Cli.Availability;

internal sealed record AvailabilityListDocument(string Bot, IReadOnlyList<AvailabilityItem> Resources, AvailabilityDelivery Delivery);
