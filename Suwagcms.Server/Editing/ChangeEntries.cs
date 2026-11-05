using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Server.Editing;

// Why: an entry holds the changed fields only, read off the stored form of the resource, so a new field of any kind is recorded without a line here.
internal static class ChangeEntries
{
    // Why: the id or name is already what the entry is filed under, and it never changes.
    private static readonly string[] Names = ["id", "name"];

    public static List<HistoryStore.Recorded> Of(
        IEnumerable<ResourceRef> touched,
        Dictionary<ResourceRef, StoredResource> before,
        Dictionary<ResourceRef, StoredResource> after
    )
    {
        var entries = touched
            .Select(target => Of(target, before.GetValueOrDefault(target), after.GetValueOrDefault(target)))
            .OfType<HistoryStore.Recorded>()
            .ToList();
        // Why: a batch is what tells apart the entries of one file; a single entry has nothing to be told apart from.
        var batch = entries.Count > 1 ? Guid.NewGuid() : (Guid?)null;
        return [.. entries.Select(entry => entry with { Batch = batch })];
    }

    private static HistoryStore.Recorded? Of(ResourceRef target, StoredResource? was, StoredResource? becomes)
    {
        var changes = Changed(Fields(was), Fields(becomes));
        if (changes.Count == 0)
        {
            return null;
        }
        var action =
            was is null ? "added"
            : becomes is null ? "deleted"
            : "edited";
        return new HistoryStore.Recorded(ResourceKinds.Word(target.Kind), target.Id, action, becomes?.Revision, changes);
    }

    private static JsonObject Fields(StoredResource? resource) =>
        resource is null ? [] : JsonNode.Parse(ResourceKinds.Write(resource.Record))?.AsObject() ?? [];

    private static List<FieldChange> Changed(JsonObject old, JsonObject fresh) =>
        [
            .. old.Select(field => field.Key)
                .Concat(fresh.Select(field => field.Key))
                .Distinct(StringComparer.Ordinal)
                .Where(name => !Names.Contains(name, StringComparer.Ordinal) && !JsonNode.DeepEquals(old[name], fresh[name]))
                .Order(StringComparer.Ordinal)
                .Select(name => new FieldChange(name, Element(old[name]), Element(fresh[name]))),
        ];

    private static JsonElement? Element(JsonNode? node) => node is null ? null : JsonSerializer.SerializeToElement(node);
}
