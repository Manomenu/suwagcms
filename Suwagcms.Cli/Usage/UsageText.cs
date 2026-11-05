using Suwagcms.Contracts;

namespace Suwagcms.Cli.Usage;

internal static class UsageText
{
    public static string Lists(UsageView usage) => $"Used by:  {Line(usage.UsedBy)}{Environment.NewLine}Uses:     {Line(usage.Uses)}";

    private static string Line(IReadOnlyList<UsageLink> links) =>
        links.Count == 0 ? "nothing" : string.Join(", ", links.Select(link => $"{link.Kind} {link.Id} ({link.Field})"));
}
