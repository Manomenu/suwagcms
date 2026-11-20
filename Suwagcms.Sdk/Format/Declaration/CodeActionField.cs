namespace Suwagcms.Sdk.Format.Declaration;

public sealed record CodeActionField
{
    public string Name { get; init; } = "";

    public string Type { get; init; } = "";

    public string Description { get; init; } = "";

    public IReadOnlyList<string>? Values { get; init; }

    public int? Min { get; init; }

    public int? Max { get; init; }

    public int? MaxLength { get; init; }
}
