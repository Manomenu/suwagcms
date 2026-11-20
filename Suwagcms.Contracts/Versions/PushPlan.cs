using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts.Versions;

public sealed record PushPlan(
    int Version,
    int? BotHas,
    PushWay Way,
    string? Problem,
    IReadOnlyList<Mistake> Mistakes,
    VersionComparison? Changes
);
