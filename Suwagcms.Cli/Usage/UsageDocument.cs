using Suwagcms.Contracts;

namespace Suwagcms.Cli.Usage;

internal sealed record UsageDocument(string Bot, string Kind, string Id, IReadOnlyList<UsageLink> UsedBy, IReadOnlyList<UsageLink> Uses);
