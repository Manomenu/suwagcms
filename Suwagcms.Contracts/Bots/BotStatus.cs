using Suwagcms.Contracts.Versions;

namespace Suwagcms.Contracts.Bots;

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
