namespace Suwagcms.Sdk.Format;

public static class ArticleLanguages
{
    public const string Polish = "pl";
    public const string English = "en";

    public static IReadOnlyList<string> All { get; } = [Polish, English];
}
