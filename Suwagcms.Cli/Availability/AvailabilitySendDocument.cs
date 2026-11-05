using Suwagcms.Contracts;

namespace Suwagcms.Cli.Availability;

internal sealed record AvailabilitySendDocument(string Bot, AvailabilityDelivery Delivery);
