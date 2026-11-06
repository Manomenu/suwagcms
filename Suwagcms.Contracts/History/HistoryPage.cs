using System.Text.Json.Serialization;

namespace Suwagcms.Contracts.History;

/// <summary>A page of history, newest first. Next is the `before` that asks for the next page, empty on the last; Since is when history started, so an empty page can say no change has been recorded since then; Total is how many entries there are in all, answered only when a numbered `page` was asked for.</summary>
public sealed record HistoryPage(
    IReadOnlyList<HistoryEntry> Entries,
    long? Next,
    DateTimeOffset Since,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? Total = null
);
