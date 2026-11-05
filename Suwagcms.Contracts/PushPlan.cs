using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts;

/// <summary>What a push of a version would do, with nothing changed: the way it would go, the version the bot reports, what the push would be refused for (<c>Problem</c> and <c>Mistakes</c>, empty when it would go through), and what going from the bot's version to this one changes (null when the two cannot be compared).</summary>
public sealed record PushPlan(
    int Version,
    int? BotHas,
    PushWay Way,
    string? Problem,
    IReadOnlyList<Mistake> Mistakes,
    VersionComparison? Changes
);
