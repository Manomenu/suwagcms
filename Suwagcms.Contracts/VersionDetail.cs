namespace Suwagcms.Contracts;

/// <summary>One version with its document exactly as stored: the JSON a bot receives, whose sha256 can be checked against it.</summary>
public sealed record VersionDetail(VersionSummary Version, string Document);
