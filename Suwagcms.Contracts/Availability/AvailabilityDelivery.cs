namespace Suwagcms.Contracts.Availability;

/// <summary>Whether the bot has the CMS's setting, from the bot's own answer: the CMS's <c>Number</c>, the one the bot reports (null when it gave none), and when not delivered the <c>Reason</c>, which says what to do.</summary>
public sealed record AvailabilityDelivery(AvailabilityDeliveryState State, int Number, int? BotNumber, string? Reason);
