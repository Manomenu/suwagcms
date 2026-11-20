using System.Text.Json.Serialization;

namespace Suwagcms.Contracts.History;

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
