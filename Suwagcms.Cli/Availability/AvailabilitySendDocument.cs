using Suwagcms.Contracts.Availability;

namespace Suwagcms.Cli.Availability;

internal sealed record AvailabilitySendDocument(string Bot, AvailabilityDelivery Delivery);
