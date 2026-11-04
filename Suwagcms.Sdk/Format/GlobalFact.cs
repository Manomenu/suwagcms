namespace Suwagcms.Sdk.Format;

public sealed record GlobalFact
{
    public required string Name { get; init; }

    public required string Type { get; init; }

    public required string Description { get; init; }

    public string WritableBy { get; init; } = FactWriters.Set;

    public IReadOnlyList<string>? Values { get; init; }

    public int? Min { get; init; }

    public int? Max { get; init; }

    public int? MaxLength { get; init; }
}
