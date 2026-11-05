namespace Suwagcms.Sdk.Format;

// Why: a parameter or a result is typed like a fact, so the rules of facts judge it; every field has a default so that a bad answer lists all its mistakes instead of stopping at the first.
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
