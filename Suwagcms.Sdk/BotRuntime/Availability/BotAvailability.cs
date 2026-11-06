namespace Suwagcms.Sdk.BotRuntime.Availability;

// Why: a message holds everything that is off, never one change, so a missed message repairs itself with the next.
public sealed record BotAvailability
{
    public static BotAvailability Nothing { get; } = new() { Number = 0, Off = [] };

    public required int Number { get; init; }

    public required IReadOnlyList<OffResource> Off { get; init; }
}
