namespace Suwagcms.Contracts.Versions;

public sealed record FieldDifference(string Field, string? Old, string? New, IReadOnlyList<TextLine>? Lines);
