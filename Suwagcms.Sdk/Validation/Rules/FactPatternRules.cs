using System.Globalization;
using Suwagcms.Sdk.Format.Facts;

namespace Suwagcms.Sdk.Validation.Rules;

// Why: a pattern on another type is one mistake, the foreign constraint, so these rules look only at a text fact's pattern.
internal static class FactPatternRules
{
    public static IEnumerable<Mistake> RegexMissing(GlobalFact fact)
    {
        if (PatternOf(fact) is { Regex.Length: 0 })
        {
            yield return FactRules.Make(
                MistakeCodes.FactPatternRegexMissing,
                fact,
                "pattern.regex",
                $"The pattern of '{fact.Name}' has no regex.",
                "Write the regular expression a whole value must match, or remove the pattern."
            );
        }
    }

    public static IEnumerable<Mistake> RegexInvalid(GlobalFact fact)
    {
        if (PatternOf(fact) is { Regex.Length: > 0 } pattern && FactValues.RegexProblem(pattern.Regex) is { } problem)
        {
            yield return FactRules.Make(
                MistakeCodes.FactPatternRegexInvalid,
                fact,
                "pattern.regex",
                $"The pattern of '{fact.Name}' cannot be used: {problem}",
                "Write a regular expression without lookarounds, backreferences, atomic groups or conditionals."
            );
        }
    }

    public static IEnumerable<Mistake> RegexMatchesEmpty(GlobalFact fact)
    {
        if (Usable(fact) is { } pattern && FactValues.Matches(pattern.Regex, ""))
        {
            yield return FactRules.Make(
                MistakeCodes.FactPatternRegexMatchesEmpty,
                fact,
                "pattern.regex",
                $"The pattern of '{fact.Name}' can match an empty text.",
                "Make at least one part required, like [A-Z]{1,3} instead of [A-Z]{0,3}."
            );
        }
    }

    public static IEnumerable<Mistake> MeansMissing(GlobalFact fact)
    {
        if (PatternOf(fact) is { } pattern && string.IsNullOrWhiteSpace(pattern.Means))
        {
            yield return FactRules.Make(
                MistakeCodes.FactPatternMeansMissing,
                fact,
                "pattern.means",
                $"The pattern of '{fact.Name}' does not say what it means.",
                "Say in words what a value looks like."
            );
        }
    }

    public static IEnumerable<Mistake> ExampleBlank(GlobalFact fact)
    {
        if (PatternOf(fact) is { Example: { } example } && string.IsNullOrWhiteSpace(example))
        {
            yield return FactRules.Make(
                MistakeCodes.FactPatternExampleBlank,
                fact,
                "pattern.example",
                $"The example of the pattern of '{fact.Name}' is blank.",
                "Write a value the pattern accepts, or remove the example."
            );
        }
    }

    public static IEnumerable<Mistake> ExampleMismatch(GlobalFact fact)
    {
        if (
            Usable(fact) is { Example: { } example } pattern
            && !string.IsNullOrWhiteSpace(example)
            && !FactValues.Matches(pattern.Regex, example)
        )
        {
            yield return FactRules.Make(
                MistakeCodes.FactPatternExampleMismatch,
                fact,
                "pattern.example",
                $"The example '{example}' of '{fact.Name}' does not match its regex {pattern.Regex}.",
                "Write an example the regex accepts, or change the regex."
            );
        }
    }

    // Why: an invalid maxLength has its own mistake, so an example is not measured against it.
    public static IEnumerable<Mistake> ExampleTooLong(GlobalFact fact)
    {
        if (
            PatternOf(fact) is { Example: { } example }
            && !string.IsNullOrWhiteSpace(example)
            && fact.MaxLength is > 0 and var limit
            && example.EnumerateRunes().Count() > limit
        )
        {
            yield return FactRules.Make(
                MistakeCodes.FactPatternExampleTooLong,
                fact,
                "pattern.example",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The example '{example}' of '{fact.Name}' is longer than its maxLength {limit}."
                ),
                "Write a shorter example, or raise maxLength."
            );
        }
    }

    public static IEnumerable<Mistake> TellCustomerInvalid(GlobalFact fact)
    {
        if (PatternOf(fact) is { } pattern && !TellCustomers.All.Contains(pattern.TellCustomer, StringComparer.Ordinal))
        {
            yield return FactRules.Make(
                MistakeCodes.FactPatternTellCustomerInvalid,
                fact,
                "pattern.tellCustomer",
                $"The pattern of '{fact.Name}' has the unknown tellCustomer '{pattern.TellCustomer}'.",
                $"Use one of: {string.Join(", ", TellCustomers.All)}."
            );
        }
    }

    private static TextPattern? PatternOf(GlobalFact fact) => fact.Type == FactTypes.Text ? fact.Pattern : null;

    private static TextPattern? Usable(GlobalFact fact) =>
        PatternOf(fact) is { Regex.Length: > 0 } pattern && FactValues.RegexProblem(pattern.Regex) is null ? pattern : null;
}
