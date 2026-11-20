namespace Suwagcms.Contracts.Versions;

public sealed record PartDifference(string Part, string Name, Difference Difference, IReadOnlyList<FieldDifference> Fields);
