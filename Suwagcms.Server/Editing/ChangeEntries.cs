using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Server.Editing;

// Why: an entry holds the changed fields only, read off the stored form of the resource, so a new field of any kind is recorded without a line here.
internal static class ChangeEntries
{
    // Why: the id or name is what an entry is filed under, so it is a change of its own only in the entry of a rename.
    private static readonly string[] Names = ["id", "name"];

    // Why: both sides are matched by row, so a rename is one entry for its resource and a rewritten referrer one for itself, never a delete and an add.
    public static List<HistoryStore.Recorded> Of(
        Dictionary<ResourceRef, StoredResource> before,
        Dictionary<ResourceRef, StoredResource> after
    )
    {
        var was = before.Values.ToDictionary(item => item.Id);
        var rows = after.Values.Select(item => item.Id).ToHashSet();
        var entries = before
            .Values.Where(item => !rows.Contains(item.Id))
            .Select(item => Of(item, null))
            .Concat(
                after
                    .Values.Where(item => !ReferenceEquals(item, before.GetValueOrDefault(item.Target)))
                    .Select(item => Of(was.GetValueOrDefault(item.Id), item))
            )
            .OfType<HistoryStore.Recorded>()
            .ToList();
        // Why: a batch is what tells apart the entries of one file; a single entry has nothing to be told apart from.
        var batch = entries.Count > 1 ? Guid.NewGuid() : (Guid?)null;
        return [.. entries.Select(entry => entry with { Batch = batch })];
    }

    private static HistoryStore.Recorded? Of(StoredResource? was, StoredResource? becomes)
    {
        var resource = becomes ?? was ?? throw new ArgumentException("an entry is about a resource");
        var renamed = was is not null && becomes is not null && was.Target != becomes.Target;
        var changes = Changed(Fields(was), Fields(becomes), renamed);
        if (changes.Count == 0)
        {
            return null;
        }
        var action =
            was is null ? "added"
            : becomes is null ? "deleted"
            : renamed ? "renamed"
            : "edited";
        return new HistoryStore.Recorded(
            ResourceKinds.Word(resource.Target.Kind),
            resource.Target.Id,
            action,
            becomes?.Revision,
            changes,
            Resource: resource.Id
        );
    }

    private static JsonObject Fields(StoredResource? resource)
    {
        var fields = resource is null ? [] : JsonNode.Parse(ResourceKinds.Write(resource.Record))?.AsObject() ?? [];
        return resource?.Record is Persona ? PersonaFields(fields) : fields;
    }

    // Why: each part of the scope is a text of its own, so an entry names the part and its lines, not one object holding all three.
    private static JsonObject PersonaFields(JsonObject stored)
    {
        var flat = new JsonObject();
        foreach (var (name, value) in stored)
        {
            if (value is JsonObject parts)
            {
                foreach (var (part, text) in parts)
                {
                    flat[$"{name}.{part}"] = text?.DeepClone();
                }
            }
            else
            {
                flat[name] = value?.DeepClone();
            }
        }
        return flat;
    }

    private static List<FieldChange> Changed(JsonObject old, JsonObject fresh, bool renamed) =>
        [
            .. old.Select(field => field.Key)
                .Concat(fresh.Select(field => field.Key))
                .Distinct(StringComparer.Ordinal)
                .Where(name => (renamed || !Names.Contains(name, StringComparer.Ordinal)) && !JsonNode.DeepEquals(old[name], fresh[name]))
                .Order(StringComparer.Ordinal)
                .Select(name => new FieldChange(name, Element(old[name]), Element(fresh[name]))),
        ];

    private static JsonElement? Element(JsonNode? node) => node is null ? null : JsonSerializer.SerializeToElement(node);
}
