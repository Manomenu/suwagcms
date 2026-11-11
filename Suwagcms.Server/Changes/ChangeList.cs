using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Contracts;
using Suwagcms.Contracts.Actions;
using Suwagcms.Contracts.Articles;
using Suwagcms.Contracts.Changes;
using Suwagcms.Contracts.Facts;
using Suwagcms.Contracts.KnowledgeBases;
using Suwagcms.Contracts.Procedures;
using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.Articles;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.KnowledgeBases;
using Suwagcms.Sdk.Format.Personas;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Actions;
using Suwagcms.Server.Articles;
using Suwagcms.Server.Editing;
using Suwagcms.Server.Facts;
using Suwagcms.Server.KnowledgeBases;
using Suwagcms.Server.Personas;
using Suwagcms.Server.Procedures;

namespace Suwagcms.Server.Changes;

// Why: a request becomes the changes the editor knows, so a file has no rule of its own; what is wrong with its shape comes first and all at once, as an unreadable body does on a single save.
internal static class ChangeList
{
    public const string OtherBot = "changes.otherBot";

    private const string Add = "add";
    private const string Edit = "edit";
    private const string Delete = "delete";
    private const string IfRevision = "ifRevision";

    private static readonly string[] Verbs = [Add, Edit, Delete];

    private static readonly string[] TopFields = ["bot", "changes"];

    private static readonly Dictionary<string, ResourceKind> Kinds = new(StringComparer.Ordinal)
    {
        [ResourceKinds.Word(ResourceKind.GlobalFact)] = ResourceKind.GlobalFact,
        [ResourceKinds.Word(ResourceKind.KnowledgeBase)] = ResourceKind.KnowledgeBase,
        [ResourceKinds.Word(ResourceKind.Article)] = ResourceKind.Article,
        [ResourceKinds.Word(ResourceKind.Action)] = ResourceKind.Action,
        [ResourceKinds.Word(ResourceKind.Procedure)] = ResourceKind.Procedure,
        [ResourceKinds.Word(ResourceKind.Persona)] = ResourceKind.Persona,
    };

    public static async Task<(List<ChangeItem> Items, List<ChangeMistake> Mistakes)> ReadAsync(
        Stream body,
        string bot,
        bool replace,
        CancellationToken cancellationToken
    )
    {
        var whole = new ResourceRef(ResourceKind.Definition, bot);
        JsonNode? root;
        try
        {
            root = await JsonNode.ParseAsync(body, cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            return ([], [At(null, Invalid(whole, "$", $"The body is not valid JSON: {exception.Message}", "Send one JSON object."))]);
        }
        if (root is not JsonObject top)
        {
            return ([], [At(null, Invalid(whole, "$", "The body is not a JSON object.", "Send one JSON object with `changes`."))]);
        }
        var mistakes = top.Where(field => !TopFields.Contains(field.Key, StringComparer.Ordinal))
            .Select(field =>
                At(
                    null,
                    new Mistake(
                        StrictBody.UnknownField,
                        ValidationStage.Save,
                        whole,
                        field.Key,
                        $"'{field.Key}' is not a field of a file of changes.",
                        $"Remove '{field.Key}', or fix its spelling: the fields are {string.Join(", ", TopFields)}.",
                        []
                    )
                )
            )
            .ToList();
        if (top["bot"] is { } named && (named.GetValueKind() != JsonValueKind.String || named.GetValue<string>() != bot))
        {
            mistakes.Add(
                At(
                    null,
                    new Mistake(
                        OtherBot,
                        ValidationStage.Save,
                        whole,
                        "bot",
                        $"This file is written for bot '{named.ToJsonString().Trim('"')}', and the bot in use is '{bot}'.",
                        $"Use the bot '{named.ToJsonString().Trim('"')}' with --bot, or change `bot` in the file if it is really meant for '{bot}'.",
                        []
                    )
                )
            );
        }
        if (top["changes"] is not JsonArray list)
        {
            mistakes.Add(
                At(
                    null,
                    Invalid(whole, "changes", "There is no `changes` list.", "Give `changes`: a list of add, edit and delete changes.")
                )
            );
            return ([], mistakes);
        }

        var items = new List<ChangeItem>();
        foreach (var (node, index) in list.Select((node, index) => (node, index)))
        {
            var (item, found) = Item(node, index + 1, whole, replace);
            mistakes.AddRange(found);
            if (item is not null)
            {
                items.Add(item);
            }
        }
        return (items, mistakes);
    }

    public static ChangeMistake At(int? position, Mistake mistake) =>
        new(position, mistake.Code, mistake.Stage, mistake.Resource, mistake.Path, mistake.Message, mistake.Fix, mistake.Related);

    private static (ChangeItem? Item, List<ChangeMistake> Mistakes) Item(JsonNode? node, int position, ResourceRef whole, bool replace)
    {
        List<ChangeMistake> Refused(string message, string fix)
        {
            var number = position.ToString(CultureInfo.InvariantCulture);
            return [At(position, Invalid(whole, $"changes[{number}]", $"Change {number}: {message}", fix))];
        }

        if (node is not JsonObject change)
        {
            return (
                null,
                Refused("it is not a mapping.", "Write it as `add: <kind>`, `edit: <kind>` or `delete: <kind>`, with its id and fields.")
            );
        }
        var verbs = Verbs.Where(change.ContainsKey).ToList();
        if (verbs.Count != 1)
        {
            return (
                null,
                Refused("it must say one of add, edit or delete.", "Give exactly one of `add: <kind>`, `edit: <kind>`, `delete: <kind>`.")
            );
        }
        var verb = verbs[0];
        var kindWord = Text(change[verb]);
        if (kindWord is null || !Kinds.TryGetValue(kindWord, out var kind))
        {
            return (
                null,
                Refused($"'{change[verb]?.ToJsonString().Trim('"')}' is not a kind.", $"The kinds are {string.Join(", ", Kinds.Keys)}.")
            );
        }
        if (kind == ResourceKind.Persona && verb != Edit)
        {
            return (
                null,
                Refused(
                    "a persona is never added or deleted: every bot has one.",
                    "Write `edit: persona`; to remove what it says, clear its fields."
                )
            );
        }
        var idField = kind == ResourceKind.GlobalFact ? "name" : "id";
        var given = kind == ResourceKind.Persona ? Persona.PublicId : Text(change[idField]);
        if (given is not { Length: > 0 } id)
        {
            return (null, Refused($"a {kindWord} needs its `{idField}`.", $"Give `{idField}`: the {kindWord}'s {idField}."));
        }

        var target = new ResourceRef(kind, id);
        int? ifRevision = null;
        if (change[IfRevision] is { } revision)
        {
            if (revision is not JsonValue value || !value.TryGetValue<int>(out var read))
            {
                return (
                    null,
                    Refused("`ifRevision` is not a whole number.", "Give the revision you read as a whole number, or leave it out.")
                );
            }
            ifRevision = read;
        }

        var fields = change.DeepClone().AsObject();
        fields.Remove(verb);
        // Why: a persona's id is not the writer's to give, so an `id` on one is an unknown field like any other.
        if (kind != ResourceKind.Persona)
        {
            fields.Remove(idField);
        }
        fields.Remove(IfRevision);
        if (verb == Add && change.ContainsKey(IfRevision))
        {
            return (
                null,
                Refused("an add has no `ifRevision`: a new resource has no revision yet.", "Remove `ifRevision`, or make this an `edit`.")
            );
        }
        if (verb == Delete)
        {
            var extra = fields.Select(field => At(position, StrictBody.Unknown(target, field.Key, ""))).ToList();
            if (extra.Count > 0)
            {
                return (null, extra);
            }
            return kind == ResourceKind.Procedure && id == Procedure.ConversationId
                ? (null, [At(position, ProcedureEndpoints.Required(target))])
                : (new ChangeItem(position, "deleted", kindWord, new Remove(target, ifRevision)), []);
        }

        var element = JsonSerializer.SerializeToElement(fields);
        var add =
            verb != Add ? (Func<object, Change>?)null
            : replace ? record => new Replace(target, record, null)
            : record => new Put(target, record, 0);
        var (made, unreadable) = kind switch
        {
            ResourceKind.GlobalFact => Made<SaveFact, GlobalFact>(
                add,
                target,
                element,
                ifRevision,
                body => FactMapping.ToRecord(id, body),
                // Why: every other field of an edit left out or null stays, and a pattern has no other way to be taken off in a file of changes.
                (stored, body) =>
                    EditOverlay.OverFact(
                        stored,
                        body,
                        element.TryGetProperty("pattern", out var pattern) && pattern.ValueKind == JsonValueKind.Null
                    )
            ),
            ResourceKind.KnowledgeBase => Made<SaveKnowledgeBase, KnowledgeBase>(
                add,
                target,
                element,
                ifRevision,
                body => KnowledgeBaseMapping.ToRecord(id, body),
                EditOverlay.Over
            ),
            ResourceKind.Article => Made<SaveArticle, Article>(
                add,
                target,
                element,
                ifRevision,
                body => ArticleMapping.ToRecord(id, body),
                EditOverlay.Over
            ),
            ResourceKind.Procedure => Made<SaveProcedure, Procedure>(
                add,
                target,
                element,
                ifRevision,
                body => ProcedureMapping.ToRecord(id, body),
                EditOverlay.Over
            ),
            ResourceKind.Action => Made<SaveAction, BotAction>(
                add,
                target,
                element,
                ifRevision,
                body => ActionMapping.ToRecord(id, body),
                EditOverlay.Over
            ),
            ResourceKind.Persona => Replacing(target, element, ifRevision, replace),
            ResourceKind.Definition => throw new InvalidOperationException($"{kind} is not a kind of a file of changes."),
            _ => throw new ArgumentOutOfRangeException(nameof(node)),
        };
        return made is null
            ? (null, [.. unreadable.Select(mistake => At(position, mistake))])
            : (new ChangeItem(position, verb == Add ? "added" : "edited", kindWord, made), []);
    }

    // Why: `add` is null for an edit, which merges what it gives with what is stored.
    private static (Change? Change, IReadOnlyList<Mistake> Mistakes) Made<TBody, TRecord>(
        Func<object, Change>? add,
        ResourceRef target,
        JsonElement fields,
        int? ifRevision,
        Func<TBody, TRecord> record,
        Func<TRecord, TBody, TBody> merged
    )
        where TBody : class
        where TRecord : class
    {
        var (body, unreadable) = StrictBody.Read<TBody>(fields, target);
        if (body is null)
        {
            return (null, unreadable);
        }
        return add is not null
            ? (add(record(body)), [])
            : (new Edit(target, stored => record(merged((TRecord)stored, body)), ifRevision), []);
    }

    // Why: an edit of a persona replaces the whole of it, as its own command does, and works on one never saved, which has no row for an Edit to find; with replace it is a replacement like any other, so one that changes nothing leaves the persona as it is.
    private static (Change? Change, IReadOnlyList<Mistake> Mistakes) Replacing(
        ResourceRef target,
        JsonElement fields,
        int? ifRevision,
        bool replace
    )
    {
        var (body, unreadable) = PersonaMapping.Read(fields, target);
        if (body is null)
        {
            return (null, unreadable);
        }
        var record = PersonaMapping.ToRecord(body);
        return replace ? (new Replace(target, record, ifRevision), []) : (new Put(target, record, ifRevision), []);
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static Mistake Invalid(ResourceRef whole, string path, string message, string fix) =>
        new(StrictBody.Invalid, ValidationStage.Save, whole, path, message, fix, []);
}
