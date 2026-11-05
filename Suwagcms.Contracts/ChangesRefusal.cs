namespace Suwagcms.Contracts;

/// <summary>The Problem Details of a refused file of changes: every mistake at once, each with its change's position. Nothing was saved.</summary>
public sealed record ChangesRefusal(string Title, int Status, string Detail, IReadOnlyList<ChangeMistake> Mistakes);
