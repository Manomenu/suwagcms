namespace Suwagcms.Sdk.Format;

public static class FactTypes
{
    public const string Enum = "enum";
    public const string WholeNumber = "int";
    public const string Text = "text";

    public static IReadOnlyList<string> All { get; } = [Enum, WholeNumber, Text];
}
