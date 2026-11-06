namespace Suwagcms.Contracts.Renames;

/// <summary>The result of changing an id, or with a dry run what it would be: the resource's kind, its old and new id, its revision, and each field of each referrer rewritten.</summary>
public sealed record RenameAnswer(string Kind, string OldId, string Id, int Revision, IReadOnlyList<RenameReferrer> Referrers);
