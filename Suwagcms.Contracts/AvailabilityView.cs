namespace Suwagcms.Contracts;

/// <summary>The bot's setting in the CMS: its number, every resource that has availability, and whether the bot has it.</summary>
public sealed record AvailabilityView(int Number, IReadOnlyList<AvailabilityItem> Resources, AvailabilityDelivery Delivery);
