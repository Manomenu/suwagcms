using System.Text.RegularExpressions;

namespace Suwagcms.Sdk.Validation;

internal static partial class ResourceIds
{
    [GeneratedRegex("^[a-z][a-zA-Z0-9]*$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CamelCasePattern();

    [GeneratedRegex("[^a-zA-Z0-9]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Separators();

    public static bool IsCamelCase(string id) => CamelCasePattern().IsMatch(id);

    // Why: a refusal names the fix, so it proposes the nearest valid id instead of only saying the id is wrong.
    public static string Suggest(string id)
    {
        var words = Separators().Split(id).Where(word => word.Length > 0).ToArray();
        if (words.Length == 0)
        {
            return "name";
        }
        var first = char.ToLowerInvariant(words[0][0]) + words[0][1..];
        var rest = string.Concat(words.Skip(1).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));
        return char.IsAsciiLetter(first[0]) ? first + rest : "n" + first + rest;
    }
}
