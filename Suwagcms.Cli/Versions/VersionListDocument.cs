using Suwagcms.Contracts.Versions;

namespace Suwagcms.Cli.Versions;

internal sealed record VersionListDocument(string Bot, IReadOnlyList<VersionSummary> Versions);
