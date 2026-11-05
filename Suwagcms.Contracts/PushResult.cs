using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts;

/// <summary>The answer to a push: how it ended, the version the bot reported afterwards (null when it did not answer), and when it failed, the reason and every mistake the bot named.</summary>
public sealed record PushResult(PushOutcome Outcome, int Version, int? BotHas, string? Reason, IReadOnlyList<Mistake> Mistakes);
