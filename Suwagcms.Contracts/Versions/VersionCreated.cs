namespace Suwagcms.Contracts.Versions;

/// <summary>The answer to creating a version: the version, and whether it was stored (false on a dry run, where the number and the sha256 are what it would get).</summary>
public sealed record VersionCreated(VersionSummary Version, bool Written);
