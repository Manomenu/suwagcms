namespace Suwagcms.Contracts;

/// <summary>A page of history, newest first. Next is the `before` that asks for the next page, empty on the last; Since is when history started, so an empty page can say no change has been recorded since then.</summary>
public sealed record HistoryPage(IReadOnlyList<HistoryEntry> Entries, long? Next, DateTimeOffset Since);
