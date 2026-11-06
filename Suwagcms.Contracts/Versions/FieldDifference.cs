namespace Suwagcms.Contracts.Versions;

/// <summary>A changed field: its old and new values (null when it had none), or for a long text its lines removed and added, with a few around them.</summary>
public sealed record FieldDifference(string Field, string? Old, string? New, IReadOnlyList<TextLine>? Lines);
