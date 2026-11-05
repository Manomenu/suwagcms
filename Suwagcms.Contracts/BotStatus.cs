namespace Suwagcms.Contracts;

/// <summary>
/// What the bot itself reports (never what the CMS tried to send), and what the CMS knows beside it.
/// <c>Has</c>, <c>Held</c> and <c>InUse</c> are the bot's answer and are empty unless <c>Reach</c> is <c>Answered</c>;
/// <c>LastKnown</c> is the number it reported the last time it answered, at <c>LastKnownAt</c>.
/// </summary>
public sealed record BotStatus(
    string Bot,
    string Address,
    BotReach Reach,
    int? Has,
    IReadOnlyList<int> Held,
    IReadOnlyList<int> InUse,
    int? LastKnown,
    DateTimeOffset? LastKnownAt,
    string? Reason,
    string? OtherBot,
    int? Latest,
    bool HasUnknownVersion,
    IReadOnlyList<PushAttempt> Attempts
);
