using System.Globalization;
using Suwagcms.Contracts;

namespace Suwagcms.Cli.KnowledgeBases;

internal static class KnowledgeBaseText
{
    // Why: a bot with no bases prints nothing, so a script can tell "none" from a line it has to parse.
    public static string Table(IReadOnlyList<KnowledgeBaseView> bases)
    {
        ArgumentNullException.ThrowIfNull(bases);
        if (bases.Count == 0)
        {
            return "";
        }
        var width = bases.Max(item => item.Id.Length);
        return string.Join(Environment.NewLine, bases.Select(item => $"{item.Id.PadRight(width)}  {item.Language}  {item.Title}"));
    }

    public static string Detail(KnowledgeBaseView item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return string.Join(
            Environment.NewLine,
            item.Id,
            $"  title:    {item.Title}",
            $"  language: {item.Language}",
            string.Create(CultureInfo.InvariantCulture, $"  revision: {item.Revision}")
        );
    }
}
