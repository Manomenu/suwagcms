using System.Globalization;
using System.Text.RegularExpressions;

namespace Suwagcms.Sdk.Format.Facts;

public static class FactValues
{
    private const RegexOptions PatternOptions = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;

    public static bool Accepts(GlobalFact fact, string value)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentNullException.ThrowIfNull(value);
        return fact.Type switch
        {
            FactTypes.Enum => fact.Values?.Contains(value, StringComparer.Ordinal) ?? false,
            FactTypes.WholeNumber => Parse(value) is { } number
                && number >= (fact.Min ?? int.MinValue)
                && number <= (fact.Max ?? int.MaxValue),
            FactTypes.Text => value.EnumerateRunes().Any()
                && value.EnumerateRunes().Count() <= (fact.MaxLength ?? int.MaxValue)
                && (fact.Pattern is null || Matches(fact.Pattern.Regex, value)),
            _ => true,
        };
    }

    public static string Valid(GlobalFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return fact.Type switch
        {
            FactTypes.Enum => $"Use one of: {string.Join(", ", fact.Values ?? [])}.",
            FactTypes.WholeNumber => string.Create(CultureInfo.InvariantCulture, $"Use a whole number from {fact.Min} to {fact.Max}."),
            FactTypes.Text => string.Create(CultureInfo.InvariantCulture, $"Use a non-empty text of at most {fact.MaxLength} characters")
                + (fact.Pattern is null ? "." : $", written as the pattern {fact.Pattern.Regex}."),
            _ => "Use a value the fact can take.",
        };
    }

    internal static int? Parse(string value) =>
        int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : null;

    internal static bool Matches(string regex, string value)
    {
        try
        {
            return Regex.IsMatch(value, WholeOf(regex), PatternOptions);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException)
        {
            return true;
        }
    }

    internal static string? RegexProblem(string regex)
    {
        try
        {
            _ = new Regex(regex, PatternOptions);
            _ = new Regex(WholeOf(regex), PatternOptions);
            return null;
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException)
        {
            return error.Message;
        }
    }

    private static string WholeOf(string regex) => $@"\A(?:{regex})\z";
}
