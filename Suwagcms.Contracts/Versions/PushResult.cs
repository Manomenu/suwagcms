using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts.Versions;

public sealed record PushResult(PushOutcome Outcome, int Version, int? BotHas, string? Reason, IReadOnlyList<Mistake> Mistakes);
