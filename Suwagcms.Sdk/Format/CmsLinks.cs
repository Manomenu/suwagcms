using System.Text;
using System.Text.RegularExpressions;

namespace Suwagcms.Sdk.Format;

// Why: the one reader of cms:// links in Markdown, so the walker, the rules and every later feature agree on what is a link and what is code.
internal static partial class CmsLinks
{
    public const string ArticlePrefix = "cms://article/";
    public const string ProcedurePrefix = "cms://procedure/";

    public static IEnumerable<CmsLink> In(string markdown)
    {
        // Why: a save checks every article of the bot, and nearly all texts hold no cms link, so they are not scanned.
        if (!markdown.Contains("cms:", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }
        var text = WithoutCode(markdown);
        foreach (Match match in LinkPattern().Matches(text))
        {
            var target = match.Groups["target"].Value;
            if (target.StartsWith("cms:", StringComparison.OrdinalIgnoreCase))
            {
                yield return Classify(match.Groups["label"].Value, target);
            }
        }
    }

    // Why: a title holding a closing bracket or a line break cannot be a Markdown label, so that link keeps the label it has and stays a link.
    public static string Relabel(string markdown, IReadOnlyDictionary<string, string> titles)
    {
        if (!markdown.Contains("cms:", StringComparison.OrdinalIgnoreCase))
        {
            return markdown;
        }
        var result = new StringBuilder();
        var copied = 0;
        foreach (Match match in LinkPattern().Matches(WithoutCode(markdown)))
        {
            var label = match.Groups["label"];
            var link = Classify(label.Value, match.Groups["target"].Value);
            if (
                link.Form == CmsLinkForm.Article
                && titles.TryGetValue(link.Id, out var title)
                && !title.Contains(']')
                && !title.Contains('\n')
            )
            {
                result.Append(markdown, copied, label.Index - copied).Append(title);
                copied = label.Index + label.Length;
            }
        }
        return result.Append(markdown, copied, markdown.Length - copied).ToString();
    }

    private static CmsLink Classify(string label, string target)
    {
        if (target.StartsWith(ProcedurePrefix, StringComparison.Ordinal))
        {
            return new CmsLink(label, target, CmsLinkForm.Procedure, string.Empty);
        }
        if (target.StartsWith(ArticlePrefix, StringComparison.Ordinal))
        {
            var id = target[ArticlePrefix.Length..];
            if (id.Length > 0 && id.All(char.IsAsciiLetterOrDigit))
            {
                return new CmsLink(label, target, CmsLinkForm.Article, id);
            }
        }
        return new CmsLink(label, target, CmsLinkForm.Malformed, string.Empty);
    }

    // Why: a fenced block and a code span are text in Markdown, so they are blanked before links are looked for; every character keeps its place, so a link found here is at the same place in the text.
    private static string WithoutCode(string markdown)
    {
        var kept = new List<string>();
        var fence = '\0';
        var fenceLength = 0;
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var indent = line.Length - line.TrimStart(' ').Length;
            var body = line.TrimStart(' ');
            var run = body.Length == 0 ? 0 : body.TakeWhile(c => c == body[0]).Count();
            var marker = indent <= 3 && run >= 3 && body[0] is '`' or '~';
            if (fence == '\0')
            {
                if (marker && !(body[0] == '`' && body[run..].Contains('`', StringComparison.Ordinal)))
                {
                    fence = body[0];
                    fenceLength = run;
                    kept.Add(new string(' ', raw.Length));
                    continue;
                }
                kept.Add(raw);
            }
            else
            {
                if (marker && body[0] == fence && run >= fenceLength && body[run..].Trim().Length == 0)
                {
                    fence = '\0';
                }
                kept.Add(new string(' ', raw.Length));
            }
        }
        return BlankSpans(string.Join('\n', kept));
    }

    private static string BlankSpans(string text)
    {
        var result = new StringBuilder(text);
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] == '\\')
            {
                index += 2;
            }
            else if (text[index] == '`')
            {
                var open = RunLength(text, index);
                var close = ClosingRun(text, index + open, open);
                if (close < 0)
                {
                    index += open;
                    continue;
                }
                for (var at = index; at < close + open; at++)
                {
                    if (text[at] != '\n')
                    {
                        result[at] = ' ';
                    }
                }
                index = close + open;
            }
            else
            {
                index++;
            }
        }
        return result.ToString();
    }

    private static int ClosingRun(string text, int from, int length)
    {
        var index = from;
        while (index < text.Length)
        {
            if (text[index] != '`')
            {
                index++;
                continue;
            }
            var run = RunLength(text, index);
            if (run == length)
            {
                return index;
            }
            index += run;
        }
        return -1;
    }

    private static int RunLength(string text, int index)
    {
        var end = index;
        while (end < text.Length && text[end] == '`')
        {
            end++;
        }
        return end - index;
    }

    [GeneratedRegex(
        "(?<!\\\\)\\[(?<label>[^\\]\\n]*)\\]\\(\\s*<?(?<target>[^)\\s<>]*)>?(?:\\s+\"[^\"]*\")?\\s*\\)",
        RegexOptions.None,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex LinkPattern();
}
