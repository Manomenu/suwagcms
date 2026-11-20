namespace Suwagcms.Contracts.Usage;

public sealed record UsageView(IReadOnlyList<UsageLink> UsedBy, IReadOnlyList<UsageLink> Uses);
