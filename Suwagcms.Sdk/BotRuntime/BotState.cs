namespace Suwagcms.Sdk.BotRuntime;

public sealed record BotState
{
    public required string Bot { get; init; }

    public required int? Current { get; init; }

    public required IReadOnlyList<int> Held { get; init; }

    public required IReadOnlyList<int> InUse { get; init; }

    public required string? StoreError { get; init; }
}
