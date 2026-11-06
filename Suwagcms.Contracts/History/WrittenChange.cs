using Suwagcms.Contracts.Versions;

namespace Suwagcms.Contracts.History;

/// <summary>An entry's changes written as a comparison writes them: each field with its old and new values or its lines, and a procedure's steps and local facts as parts.</summary>
public sealed record WrittenChange(IReadOnlyList<FieldDifference> Fields, IReadOnlyList<PartDifference> Parts);
