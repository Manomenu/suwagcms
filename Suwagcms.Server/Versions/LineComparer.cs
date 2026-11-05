using System.Globalization;
using Suwagcms.Contracts;

namespace Suwagcms.Server.Versions;

// Why: a long text is compared by lines, so a price changed inside an article reads as that line and not as the whole article.
internal static class LineComparer
{
    private const int Around = 2;

    // Why: two whole articles that differ everywhere would need a table of 25 million cells; past this size they read as removed and added, which is still true.
    private const int LargestTable = 4_000_000;

    public static IReadOnlyList<TextLine> Compare(string? before, string? after)
    {
        var old = (before ?? "").Split('\n');
        var fresh = (after ?? "").Split('\n');
        var start = 0;
        while (start < old.Length && start < fresh.Length && old[start] == fresh[start])
        {
            start++;
        }
        var end = 0;
        while (end < old.Length - start && end < fresh.Length - start && old[old.Length - 1 - end] == fresh[fresh.Length - 1 - end])
        {
            end++;
        }
        var lines = new List<TextLine>();
        lines.AddRange(old[..start].Select(Kept));
        lines.AddRange(Middle(old[start..(old.Length - end)], fresh[start..(fresh.Length - end)]));
        lines.AddRange(old[(old.Length - end)..].Select(Kept));
        return Fold(lines);
    }

    private static TextLine Kept(string line) => new(LineChange.Kept, line);

    private static List<TextLine> Middle(string[] old, string[] fresh)
    {
        var lines = new List<TextLine>();
        if ((long)old.Length * fresh.Length > LargestTable)
        {
            lines.AddRange(old.Select(line => new TextLine(LineChange.Removed, line)));
            lines.AddRange(fresh.Select(line => new TextLine(LineChange.Added, line)));
            return lines;
        }
        var longest = new int[old.Length + 1][];
        longest[old.Length] = new int[fresh.Length + 1];
        for (var i = old.Length - 1; i >= 0; i--)
        {
            longest[i] = new int[fresh.Length + 1];
            for (var j = fresh.Length - 1; j >= 0; j--)
            {
                longest[i][j] = old[i] == fresh[j] ? longest[i + 1][j + 1] + 1 : Math.Max(longest[i + 1][j], longest[i][j + 1]);
            }
        }
        int at = 0,
            to = 0;
        while (at < old.Length || to < fresh.Length)
        {
            if (at < old.Length && to < fresh.Length && old[at] == fresh[to])
            {
                lines.Add(Kept(old[at]));
                at++;
                to++;
            }
            else if (at < old.Length && (to == fresh.Length || longest[at + 1][to] >= longest[at][to + 1]))
            {
                lines.Add(new TextLine(LineChange.Removed, old[at++]));
            }
            else
            {
                lines.Add(new TextLine(LineChange.Added, fresh[to++]));
            }
        }
        return lines;
    }

    private static List<TextLine> Fold(List<TextLine> lines)
    {
        var shown = new bool[lines.Count];
        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].Change != LineChange.Kept)
            {
                for (var near = Math.Max(0, index - Around); near <= Math.Min(lines.Count - 1, index + Around); near++)
                {
                    shown[near] = true;
                }
            }
        }
        var folded = new List<TextLine>();
        var hidden = 0;
        for (var index = 0; index < lines.Count; index++)
        {
            if (shown[index])
            {
                Flush();
                folded.Add(lines[index]);
            }
            else
            {
                hidden++;
            }
        }
        Flush();
        return folded;

        void Flush()
        {
            if (hidden > 0)
            {
                folded.Add(
                    new TextLine(
                        LineChange.Folded,
                        string.Create(CultureInfo.InvariantCulture, $"{hidden} unchanged line{(hidden == 1 ? "" : "s")}")
                    )
                );
                hidden = 0;
            }
        }
    }
}
