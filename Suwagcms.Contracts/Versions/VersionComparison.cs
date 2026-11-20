namespace Suwagcms.Contracts.Versions;

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
