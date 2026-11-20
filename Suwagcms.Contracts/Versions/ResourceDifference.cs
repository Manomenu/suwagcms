using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Contracts.Versions;

public sealed record ResourceDifference(
    ResourceKind Kind,
    string Id,
    Difference Difference,
    IReadOnlyList<FieldDifference> Fields,
    IReadOnlyList<PartDifference> Parts
);
