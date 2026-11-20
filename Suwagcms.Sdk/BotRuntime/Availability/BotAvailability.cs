namespace Suwagcms.Sdk.BotRuntime.Availability;

public sealed record BotAvailability
{
    public static BotAvailability Nothing { get; } = new() { Number = 0, Off = [] };

    public required int Number { get; init; }

    public required IReadOnlyList<OffResource> Off { get; init; }
}
