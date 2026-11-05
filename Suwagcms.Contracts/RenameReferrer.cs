namespace Suwagcms.Contracts;

/// <summary>One field of a resource rewritten by a change of id (<c>Kind</c> is article or procedure): how many links or uses it held, and the revision the resource then has.</summary>
public sealed record RenameReferrer(string Kind, string Id, string Field, int Links, int Revision);
