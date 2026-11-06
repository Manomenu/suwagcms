namespace Suwagcms.Sdk.Format.Facts;

public static class FactWriters
{
    public const string Assign = "assign";
    public const string Set = "set";
    public const string Correct = "correct";

    public static IReadOnlyList<string> All { get; } = [Assign, Set, Correct];
}
