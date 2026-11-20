using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts.Changes;

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
