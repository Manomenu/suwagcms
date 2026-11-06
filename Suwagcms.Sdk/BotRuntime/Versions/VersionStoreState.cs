using Suwagcms.Sdk.BotRuntime.Availability;

namespace Suwagcms.Sdk.BotRuntime.Versions;

public sealed record VersionStoreState(
    int? Current,
    IReadOnlyList<int> Held,
    IReadOnlyList<int>? InUse,
    BotAvailability? Availability = null
);
