namespace Suwagcms.Contracts.Renames;

public sealed record RenameReferrer(string Kind, string Id, string Field, int Links, int Revision);
