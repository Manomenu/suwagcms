using Suwagcms.Contracts.Usage;

namespace Suwagcms.Cli.Usage;

internal sealed record UsageDocument(string Bot, string Kind, string Id, IReadOnlyList<UsageLink> UsedBy, IReadOnlyList<UsageLink> Uses);
