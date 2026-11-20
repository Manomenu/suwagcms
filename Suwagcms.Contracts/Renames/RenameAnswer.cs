namespace Suwagcms.Contracts.Renames;

public sealed record RenameAnswer(string Kind, string OldId, string Id, int Revision, IReadOnlyList<RenameReferrer> Referrers);
