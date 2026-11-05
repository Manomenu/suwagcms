namespace Suwagcms.Sdk.BotRuntime;

public sealed record VersionStoreState(
    int? Current,
    IReadOnlyList<int> Held,
    IReadOnlyList<int>? InUse,
    BotAvailability? Availability = null
);
