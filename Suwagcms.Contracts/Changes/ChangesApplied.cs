using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts.Changes;

/// <summary>The answer to a file of changes: the counts, the result per change, whether anything was written, and NotReady, what still stops a version, which never refuses a file.</summary>
public sealed record ChangesApplied(
    int Added,
    int Edited,
    int Deleted,
    bool DryRun,
    IReadOnlyList<ChangeApplied> Changes,
    IReadOnlyList<Mistake> NotReady
);
