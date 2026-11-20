using System.Text.RegularExpressions;

namespace Suwagcms.Sdk.Format.Procedures;

internal static partial class SayPlaceholders
{
    [GeneratedRegex(@"\{(" + CamelCase.Word + @"(?:\." + CamelCase.Word + @")*)\}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Pattern();

    public static IEnumerable<string> Names(string text) =>
        Pattern().Matches(text).Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal);

    public static string Renamed(string text, string from, string to) =>
        Pattern().Replace(text, match => match.Groups[1].Value == from ? $"{{{to}}}" : match.Value);
}
