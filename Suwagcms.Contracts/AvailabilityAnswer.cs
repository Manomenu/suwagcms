namespace Suwagcms.Contracts;

/// <summary>The result of turning a resource on or off, or with a dry run what it would be: <c>Changed</c> is false when it already was so; <c>UsedBy</c> is what uses it, listed when turning off; <c>Delivery</c> says whether the bot has it.</summary>
public sealed record AvailabilityAnswer(
    string Kind,
    string Id,
    bool Off,
    bool Changed,
    IReadOnlyList<UsageLink> UsedBy,
    AvailabilityDelivery Delivery
);
