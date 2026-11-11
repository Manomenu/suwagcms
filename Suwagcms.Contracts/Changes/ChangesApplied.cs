using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts.Changes;

/// <summary>The answer to a file of changes: the counts, the result per change, whether anything was written, NotMentioned, what the CMS holds of the kinds the file changes that the file does not name (left as it is), and NotReady, what still stops a version, which never refuses a file.</summary>
public sealed record ChangesApplied(
    int Added,
    int Replaced,
    int Unchanged,
    int Edited,
    int Deleted,
    bool DryRun,
    IReadOnlyList<ChangeApplied> Changes,
    IReadOnlyList<UnmentionedResource> NotMentioned,
    IReadOnlyList<Mistake> NotReady
);
