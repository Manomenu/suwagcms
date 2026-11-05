using Suwagcms.Sdk.Format;

namespace Suwagcms.Contracts;

/// <summary>A resource that differs between two versions, by kind and id; only a changed one lists fields and parts.</summary>
public sealed record ResourceDifference(
    ResourceKind Kind,
    string Id,
    Difference Difference,
    IReadOnlyList<FieldDifference> Fields,
    IReadOnlyList<PartDifference> Parts
);
