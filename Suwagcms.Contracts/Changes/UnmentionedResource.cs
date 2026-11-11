namespace Suwagcms.Contracts.Changes;

/// <summary>A resource the CMS holds that a file of changes does not name, of a kind the file changes: Kind is the word a file uses, Id its id (a fact's name). A file never deletes it for being absent.</summary>
public sealed record UnmentionedResource(string Kind, string Id);
