namespace Suwagcms.Contracts;

/// <summary>One recorded change: when the server saved it, what it reached (kind: fact, knowledgeBase, article, procedure or version; and id), the action (added, edited, deleted, created or pushed), the revision it produced, the changed fields, and Batch, which the entries of one file of changes share.</summary>
public sealed record HistoryEntry(
    long Id,
    DateTimeOffset At,
    string Kind,
    string ResourceId,
    string Action,
    int? Revision,
    IReadOnlyList<FieldChange> Changes,
    Guid? Batch
);
