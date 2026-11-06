using System.Globalization;

namespace Suwagcms.Server.Settings;

// Why: a mistyped number stops the server at start, because guessing would leave every list a different length from what the owner set.
internal static class PageSize
{
    public const string Variable = "PAGINATION_PAGE_SIZE";
    public const int Most = 500;
    private const int Default = 20;

    public static int Read(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Default;
        }
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var size) && size is >= 1 and <= Most
            ? size
            : throw new InvalidOperationException(
                $"{Variable} is '{value}': set a whole number from 1 to {Most}, or remove it to use {Default}."
            );
    }
}
