using System.Text.RegularExpressions;

namespace Suwagcms.Sdk.Format;

// Why: the rules that refuse a definition and the runner that fills it must read {name} and {secret:NAME} alike, so the patterns exist once.
internal static partial class ActionTemplates
{
    public const string SecretPrefix = "secret:";

    [GeneratedRegex(@"\{([^{}]*)\}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Placeholder();

    [GeneratedRegex("^[A-Z][A-Z0-9_]*$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    public static partial Regex SecretName();

    public static IEnumerable<string> PlaceholdersIn(string text) => Placeholder().Matches(text).Select(match => match.Groups[1].Value);

    public static IEnumerable<string> SecretsIn(string text) =>
        PlaceholdersIn(text)
            .Where(found => found.StartsWith(SecretPrefix, StringComparison.Ordinal))
            .Select(found => found[SecretPrefix.Length..]);
}
