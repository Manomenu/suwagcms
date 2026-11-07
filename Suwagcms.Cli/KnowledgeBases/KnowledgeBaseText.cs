using System.Globalization;
using Suwagcms.Contracts.KnowledgeBases;

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
        var profileWidth = bases.Max(item => item.Embedding.Length);
        return string.Join(
            Environment.NewLine,
            bases.Select(item =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{item.Id.PadRight(width)}  {item.Language}  {item.Embedding.PadRight(profileWidth)}  {item.Results, 2}  {item.Title}"
                )
            )
        );
    }

    public static string Summary(KnowledgeBaseView item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return string.Create(CultureInfo.InvariantCulture, $"{item.Title}, {item.Language}, {item.Embedding}, {item.Results} results");
    }

    public static string Detail(KnowledgeBaseView item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return string.Join(
            Environment.NewLine,
            item.Id,
            $"  title:     {item.Title}",
            $"  language:  {item.Language}",
            $"  embedding: {item.Embedding}",
            string.Create(CultureInfo.InvariantCulture, $"  results:   {item.Results}"),
            string.Create(CultureInfo.InvariantCulture, $"  revision:  {item.Revision}")
        );
    }
}
