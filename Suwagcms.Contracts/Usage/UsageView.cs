namespace Suwagcms.Contracts.Usage;

/// <summary>What refers to a resource and what it refers to, each with the field that holds the reference: the lists a refused delete is made of.</summary>
public sealed record UsageView(IReadOnlyList<UsageLink> UsedBy, IReadOnlyList<UsageLink> Uses);
