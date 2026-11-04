using System.Globalization;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Validation;

// Why: a condition and an assign must agree with the fact on what values it can take, so the test of a value lives once, beside the fact.
internal static class FactValues
{
    public static bool Accepts(GlobalFact fact, string value) =>
        fact.Type switch
        {
            FactTypes.Enum => fact.Values?.Contains(value, StringComparer.Ordinal) ?? false,
            FactTypes.WholeNumber => Parse(value) is { } number
                && number >= (fact.Min ?? int.MinValue)
                && number <= (fact.Max ?? int.MaxValue),
            FactTypes.Text => value.EnumerateRunes().Any() && value.EnumerateRunes().Count() <= (fact.MaxLength ?? int.MaxValue),
            _ => true,
        };

    public static string Valid(GlobalFact fact) =>
        fact.Type switch
        {
            FactTypes.Enum => $"Use one of: {string.Join(", ", fact.Values ?? [])}.",
            FactTypes.WholeNumber => string.Create(CultureInfo.InvariantCulture, $"Use a whole number from {fact.Min} to {fact.Max}."),
            FactTypes.Text => string.Create(CultureInfo.InvariantCulture, $"Use a non-empty text of at most {fact.MaxLength} characters."),
            _ => "Use a value the fact can take.",
        };

    public static int? Parse(string value) =>
        int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : null;
}
