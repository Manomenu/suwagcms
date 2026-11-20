using System.Text.Json.Serialization;

namespace Suwagcms.Contracts.History;

public sealed record HistoryPage(
    IReadOnlyList<HistoryEntry> Entries,
    long? Next,
    DateTimeOffset Since,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? Total = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Incomplete = null
);
