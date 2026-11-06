using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Contracts.History;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.Format.Personas;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Versions;

// Why: it reads the two documents as data, not as records, so a field the format gains later shows as a difference without a change here.
internal static class VersionComparer
{
    private const string PersonaMember = "persona";

    private static readonly (string List, ResourceKind Kind, string Key)[] Kinds =
    [
        ("facts", ResourceKind.GlobalFact, "name"),
        ("knowledgeBases", ResourceKind.KnowledgeBase, "id"),
        ("articles", ResourceKind.Article, "id"),
        ("actions", ResourceKind.Action, "id"),
        ("procedures", ResourceKind.Procedure, "id"),
    ];

    private static readonly string[] NeverDifferences = ["bot", "number", "digest", PersonaMember];

    private static readonly string[] PersonaTexts = ["personality", "scope.inScope", "scope.outOfScope", "scope.whenOutOfScope"];

    private static readonly string[] LongTexts = ["content", "llmGuide"];

    private static readonly string[] ProcedureParts = ["steps", "localFacts"];

    private static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static VersionComparison Compare(string bot, int from, int to, string before, string after)
    {
        var old = Parse(before);
        var fresh = Parse(after);
        var definition = Fields(
            Without(old, NeverDifferences.Concat(Kinds.Select(kind => kind.List))),
            Without(fresh, NeverDifferences.Concat(Kinds.Select(kind => kind.List))),
            ""
        );
        var resources = Kinds
            .SelectMany(kind => Resources(kind.Kind, kind.Key, old[kind.List], fresh[kind.List]))
            .Concat(PersonaDifference(old[PersonaMember], fresh[PersonaMember]))
            .ToList();
        return new VersionComparison(
            bot,
            from,
            to,
            resources.Count(resource => resource.Difference == Difference.Added),
            resources.Count(resource => resource.Difference == Difference.Removed),
            resources.Count(resource => resource.Difference == Difference.Changed),
            definition,
            resources
        );
    }

    // Why: an entry and a comparison are written by this one code, so given the same old and new text they cannot disagree; the entry stays as it was recorded.
    public static WrittenChange Written(string kind, IReadOnlyList<FieldChange> changes)
    {
        var old = new JsonObject();
        var fresh = new JsonObject();
        foreach (var change in changes)
        {
            old[change.Field] = Node(change.Old);
            fresh[change.Field] = Node(change.New);
        }
        if (kind == ResourceKinds.Word(ResourceKind.Persona))
        {
            return new WrittenChange([.. changes.Select(change => PersonaField(change.Field, old, fresh))], []);
        }
        if (kind != ResourceKinds.Word(ResourceKind.Procedure))
        {
            return new WrittenChange(Fields(old, fresh, ""), []);
        }
        return new WrittenChange(
            Fields(Without(old, ProcedureParts), Without(fresh, ProcedureParts), ""),
            [
                .. Parts("step", old["steps"], fresh["steps"], whole: true),
                .. Parts("local fact", old["localFacts"], fresh["localFacts"], whole: true),
            ]
        );
    }

    private static JsonNode? Node(JsonElement? value) => value is { } element ? JsonNode.Parse(element.GetRawText()) : null;

    private static FieldDifference PersonaField(string field, JsonObject old, JsonObject fresh) =>
        PersonaTexts.Contains(field, StringComparer.Ordinal)
            ? new FieldDifference(field, null, null, LineComparer.Compare(Text(old[field]), Text(fresh[field])))
            : new FieldDifference(field, Text(old[field]), Text(fresh[field]), null);

    private static JsonObject Parse(string document) =>
        JsonNode.Parse(document) as JsonObject ?? throw new InvalidOperationException("A stored version is not a JSON object.");

    private static JsonObject Without(JsonObject source, IEnumerable<string> names)
    {
        var copy = (JsonObject)source.DeepClone();
        foreach (var name in names)
        {
            copy.Remove(name);
        }
        return copy;
    }

    private static Dictionary<string, JsonObject> ByKey(JsonNode? list, string key) =>
        (list as JsonArray ?? [])
            .OfType<JsonObject>()
            .ToDictionary(item => item[key]?.GetValue<string>() ?? throw new InvalidOperationException($"A resource has no {key}."));

    private static IEnumerable<ResourceDifference> Resources(ResourceKind kind, string key, JsonNode? before, JsonNode? after)
    {
        var old = ByKey(before, key);
        var fresh = ByKey(after, key);
        foreach (var id in old.Keys.Union(fresh.Keys).Order(StringComparer.Ordinal))
        {
            if (!fresh.TryGetValue(id, out var now))
            {
                yield return new ResourceDifference(kind, id, Difference.Removed, [], []);
            }
            else if (!old.TryGetValue(id, out var was))
            {
                yield return new ResourceDifference(kind, id, Difference.Added, [], []);
            }
            else if (!JsonNode.DeepEquals(was, now))
            {
                yield return kind == ResourceKind.Procedure
                    ? new ResourceDifference(
                        kind,
                        id,
                        Difference.Changed,
                        Fields(Without(was, ProcedureParts), Without(now, ProcedureParts), ""),
                        [.. Parts("step", was["steps"], now["steps"]), .. Parts("local fact", was["localFacts"], now["localFacts"])]
                    )
                    : new ResourceDifference(kind, id, Difference.Changed, Fields(was, now, ""), []);
            }
        }
    }

    // Why: one member, not a list, and its texts are what a person wrote line by line, so even a persona added or removed says its lines.
    private static IEnumerable<ResourceDifference> PersonaDifference(JsonNode? before, JsonNode? after)
    {
        if (JsonNode.DeepEquals(before, after))
        {
            yield break;
        }
        var difference =
            before is null ? Difference.Added
            : after is null ? Difference.Removed
            : Difference.Changed;
        var fields = PersonaTexts
            .Select(path => (Path: path, Old: PersonaText(before, path), New: PersonaText(after, path)))
            .Where(text => text.Old != text.New)
            .Select(text => new FieldDifference(text.Path, null, null, LineComparer.Compare(text.Old, text.New)))
            .ToList();
        yield return new ResourceDifference(ResourceKind.Persona, Persona.PublicId, difference, fields, []);
    }

    private static string? PersonaText(JsonNode? persona, string path) =>
        path.Split('.').Aggregate(persona, (node, name) => node?[name])?.GetValue<string>();

    // Why: `whole` is an entry's way: its record is all that is left of a part added or deleted, so unlike a comparison it keeps what the part held.
    private static IEnumerable<PartDifference> Parts(string part, JsonNode? before, JsonNode? after, bool whole = false)
    {
        var old = Named(before);
        var fresh = Named(after);
        foreach (var name in old.Keys.Union(fresh.Keys).Order(StringComparer.Ordinal))
        {
            if (!fresh.TryGetValue(name, out var now))
            {
                yield return new PartDifference(part, name, Difference.Removed, whole ? Fields(old[name] as JsonObject ?? [], [], "") : []);
            }
            else if (!old.TryGetValue(name, out var was))
            {
                yield return new PartDifference(part, name, Difference.Added, whole ? Fields([], now as JsonObject ?? [], "") : []);
            }
            else if (!JsonNode.DeepEquals(was, now))
            {
                yield return new PartDifference(
                    part,
                    name,
                    Difference.Changed,
                    Fields(was as JsonObject ?? [], now as JsonObject ?? [], "")
                );
            }
        }
    }

    // Why: steps are a map by name and local facts a list with a name in each, and both read as named parts.
    private static Dictionary<string, JsonNode?> Named(JsonNode? parts) =>
        parts switch
        {
            JsonObject map => map.ToDictionary(item => item.Key, item => item.Value),
            JsonArray list => list.OfType<JsonObject>()
                .ToDictionary(item => item["name"]?.GetValue<string>() ?? "", item => (JsonNode?)item),
            _ => [],
        };

    private static List<FieldDifference> Fields(JsonObject before, JsonObject after, string prefix)
    {
        var differences = new List<FieldDifference>();
        foreach (var name in before.Select(item => item.Key).Union(after.Select(item => item.Key)).Order(StringComparer.Ordinal))
        {
            var old = before[name];
            var fresh = after[name];
            var field = prefix + name;
            if (JsonNode.DeepEquals(old, fresh))
            {
                continue;
            }
            if (old is JsonObject oldObject && fresh is JsonObject freshObject)
            {
                differences.AddRange(Fields(oldObject, freshObject, $"{field}."));
            }
            else if (prefix.Length == 0 && LongTexts.Contains(name, StringComparer.Ordinal))
            {
                differences.Add(new FieldDifference(field, null, null, LineComparer.Compare(Text(old), Text(fresh))));
            }
            else
            {
                differences.Add(new FieldDifference(field, Text(old), Text(fresh), null));
            }
        }
        return differences;
    }

    private static string? Text(JsonNode? value) =>
        value switch
        {
            null => null,
            JsonValue text when text.TryGetValue<string>(out var plain) => plain,
            _ => value.ToJsonString(Compact),
        };
}
