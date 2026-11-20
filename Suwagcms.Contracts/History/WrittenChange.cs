using Suwagcms.Contracts.Versions;

namespace Suwagcms.Contracts.History;

public sealed record WrittenChange(IReadOnlyList<FieldDifference> Fields, IReadOnlyList<PartDifference> Parts);
