namespace Suwagcms.Contracts;

/// <summary>A part of a procedure that differs: a step or a local fact, by name, with what changed in it.</summary>
public sealed record PartDifference(string Part, string Name, Difference Difference, IReadOnlyList<FieldDifference> Fields);
