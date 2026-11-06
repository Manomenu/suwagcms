using Suwagcms.Contracts.Renames;

namespace Suwagcms.Cli.Renames;

internal sealed record RenameDocument(
    string Bot,
    string Kind,
    string OldId,
    string Id,
    int Revision,
    IReadOnlyList<RenameReferrer> Referrers,
    bool DryRun
);
