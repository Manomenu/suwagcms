namespace Suwagcms.Contracts;

/// <summary>What one change of a file did, or would do on a dry run. Change is its position in the file, from 1; Action is added, edited or deleted; Kind is the word the file uses; Revision is the resource's new revision, none after a delete.</summary>
public sealed record ChangeApplied(int Change, string Action, string Kind, string Id, int? Revision);
