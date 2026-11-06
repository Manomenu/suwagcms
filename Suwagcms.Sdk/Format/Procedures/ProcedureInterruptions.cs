namespace Suwagcms.Sdk.Format.Procedures;

public static class ProcedureInterruptions
{
    public const string Keep = "keep";
    public const string Discard = "discard";

    public static IReadOnlyList<string> All { get; } = [Keep, Discard];
}
