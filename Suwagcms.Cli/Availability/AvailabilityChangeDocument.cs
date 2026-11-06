using Suwagcms.Contracts.Availability;
using Suwagcms.Contracts.Usage;

namespace Suwagcms.Cli.Availability;

internal sealed record AvailabilityChangeDocument(
    string Bot,
    string Kind,
    string Id,
    bool Off,
    bool Changed,
    IReadOnlyList<UsageLink> UsedBy,
    AvailabilityDelivery Delivery,
    bool DryRun
);
