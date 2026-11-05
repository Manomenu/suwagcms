namespace Suwagcms.Sdk.BotRuntime;

public sealed record BotState
{
    public required string Bot { get; init; }

    public required int? Current { get; init; }

    public required IReadOnlyList<int> Held { get; init; }

    public required IReadOnlyList<int> InUse { get; init; }

    public required string? StoreError { get; init; }

    // Why: not required, so that the CMS still reads the state of a bot on an older SDK, which then counts as holding nothing.
    public BotAvailability Availability { get; init; } = BotAvailability.Nothing;
}
