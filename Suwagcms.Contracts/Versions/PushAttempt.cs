namespace Suwagcms.Contracts.Versions;

public sealed record PushAttempt(Guid Id, int Version, PushOutcome Outcome, string? Error, DateTimeOffset At);
