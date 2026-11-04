using System.Text.RegularExpressions;

namespace Suwagcms.Sdk.Validation;

internal static partial class FactNames
{
    [GeneratedRegex("^[a-z][a-zA-Z0-9]*$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CamelCasePattern();

    [GeneratedRegex("[^a-zA-Z0-9]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Separators();

    public static bool IsValid(string name) => name.Split('.').All(segment => CamelCasePattern().IsMatch(segment));

    // Why: a refusal names the fix, so it proposes the nearest valid name instead of only saying the name is wrong.
    public static string Suggest(string name) => string.Join('.', name.Split('.').Select(ToCamelCase));

    private static string ToCamelCase(string segment)
    {
        var words = Separators().Split(segment).Where(word => word.Length > 0).ToArray();
        if (words.Length == 0)
        {
            return "name";
        }
        var first = char.ToLowerInvariant(words[0][0]) + words[0][1..];
        var rest = string.Concat(words.Skip(1).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
        return char.IsAsciiLetter(first[0]) ? first + rest : "n" + first + rest;
    }
}
