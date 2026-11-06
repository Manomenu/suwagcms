namespace Suwagcms.Sdk.Format.Facts;

public static class TellCustomers
{
    public const string Upfront = "upfront";
    public const string AfterRefusal = "afterRefusal";

    public static IReadOnlyList<string> All { get; } = [Upfront, AfterRefusal];
}
