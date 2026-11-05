using Suwagcms.Contracts;

namespace Suwagcms.Cli.Versions;

internal sealed record VersionListDocument(string Bot, IReadOnlyList<VersionSummary> Versions);
