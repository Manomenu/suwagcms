namespace Suwagcms.Contracts;

/// <summary>What going from one version of a bot to another changes, in a fixed order: by kind, then by id. <c>Definition</c> lists what differs outside the resources, such as the format version; numbers and digests are not differences.</summary>
public sealed record VersionComparison(
    string Bot,
    int From,
    int To,
    int Added,
    int Removed,
    int Changed,
    IReadOnlyList<FieldDifference> Definition,
    IReadOnlyList<ResourceDifference> Resources
);
