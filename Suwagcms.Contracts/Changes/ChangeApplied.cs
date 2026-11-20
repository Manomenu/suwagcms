using Suwagcms.Contracts.Versions;

namespace Suwagcms.Contracts.Changes;

public sealed record ChangeApplied(
    int Change,
    string Action,
    string Kind,
    string Id,
    int? Revision,
    IReadOnlyList<FieldDifference> Fields,
    IReadOnlyList<PartDifference> Parts
);
