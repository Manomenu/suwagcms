namespace Suwagcms.Sdk.Format;

public sealed record VersionEnvelope
{
    public required int FormatVersion { get; init; }

    public required string Bot { get; init; }

    public required int Number { get; init; }

    public required string Digest { get; init; }
}
