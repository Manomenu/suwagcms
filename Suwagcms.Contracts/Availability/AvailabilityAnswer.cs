using Suwagcms.Contracts.Usage;

namespace Suwagcms.Contracts.Availability;

public sealed record AvailabilityAnswer(
    string Kind,
    string Id,
    bool Off,
    bool Changed,
    IReadOnlyList<UsageLink> UsedBy,
    AvailabilityDelivery Delivery
);
