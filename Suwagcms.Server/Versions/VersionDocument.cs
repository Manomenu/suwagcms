using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Sdk.Format;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Versions;

// Why: the one place that writes a version's document, so its order and form are fixed and its digest comes only from the SDK's function.
internal static class VersionDocument
{
    private static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static (string Json, string Sha256) Write(string bot, int number, IEnumerable<StoredResource> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var all = WithCurrentTitles([.. resources]);
        var document = new JsonObject
        {
            ["formatVersion"] = BotDefinition.CurrentFormatVersion,
            ["bot"] = bot,
            ["number"] = number,
            ["digest"] = "",
            ["facts"] = Kind(all, ResourceKind.GlobalFact),
            ["knowledgeBases"] = Kind(all, ResourceKind.KnowledgeBase),
            ["articles"] = Kind(all, ResourceKind.Article),
            ["actions"] = Kind(all, ResourceKind.Action),
            ["procedures"] = Kind(all, ResourceKind.Procedure),
        };
        var digest = VersionDigest.Of(document.ToJsonString(Compact));
        document["digest"] = digest;
        return (document.ToJsonString(Compact), digest);
    }

    private static List<StoredResource> WithCurrentTitles(List<StoredResource> resources)
    {
        var articles = resources.Select(item => item.Record).OfType<Article>().ToList();
        var titles = articles.ToDictionary(article => article.Id, article => article.Title);
        return
        [
            .. resources.Select(item =>
                item.Record is Article article ? item with { Record = ArticleLinkLabels.WithCurrentTitles(article, titles) } : item
            ),
        ];
    }

    // Why: resources are put in ordinal order of their ids whatever order they were saved or loaded in, so equal states give equal documents.
    private static JsonArray Kind(List<StoredResource> all, ResourceKind kind) =>
        new([
            .. all.Where(item => item.Target.Kind == kind)
                .OrderBy(item => item.Target.Id, StringComparer.Ordinal)
                .Select(item => JsonNode.Parse(ResourceKinds.Write(item.Record))),
        ]);
}
