namespace Suwagcms.Contracts.Versions;

/// <summary>One recorded push: the version, when it was tried, how it ended and, when it failed, the reason.</summary>
public sealed record PushAttempt(Guid Id, int Version, PushOutcome Outcome, string? Error, DateTimeOffset At);
