namespace Suwagcms.Sdk.Format;

// Why: an id, a fact name and a fact named inside a say text are built from the same word, so the rules cannot drift apart.
internal static class CamelCase
{
    public const string Word = "[a-z][a-zA-Z0-9]*";
}
