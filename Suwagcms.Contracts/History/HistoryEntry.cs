using System.Text.Json.Serialization;

namespace Suwagcms.Contracts.History;

/// <summary>One recorded change: when the server saved it, what it reached (kind: fact, knowledgeBase, article, procedure, action or version; and id), the action (added, edited, deleted, renamed, turned on, turned off, created or pushed), the revision it produced, the changed fields, and Batch, which the entries of one file of changes share. Written is the same changes as a comparison writes them, answered only when a numbered `page` was asked for.</summary>
public sealed record HistoryEntry(
    long Id,
    DateTimeOffset At,
    string Kind,
    string ResourceId,
    string Action,
    int? Revision,
    IReadOnlyList<FieldChange> Changes,
    Guid? Batch,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] WrittenChange? Written = null
);
