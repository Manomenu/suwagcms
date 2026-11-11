using Suwagcms.Contracts.Versions;

namespace Suwagcms.Contracts.Changes;

/// <summary>What one change of a file did, or would do on a dry run. Change is its position in the file, from 1; Action is added, edited or deleted, and with replace also replaced (an add of a resource the CMS holds) or unchanged (a change that would store what is already stored, so the revision stays); Kind is the word the file uses; Revision is the resource's revision afterwards, none after a delete. Fields and Parts say what differs from what the CMS held, as a comparison of two versions says it: only a replaced or edited resource lists them, and a procedure's steps and local facts are parts.</summary>
public sealed record ChangeApplied(
    int Change,
    string Action,
    string Kind,
    string Id,
    int? Revision,
    IReadOnlyList<FieldDifference> Fields,
    IReadOnlyList<PartDifference> Parts
);
