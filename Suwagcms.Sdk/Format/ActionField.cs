namespace Suwagcms.Sdk.Format;

// Why: a parameter or a result is typed like a fact and named by its key; Type and Description default to empty so that a missing one reaches the rules, which name it with the same code as for a fact.
public sealed record ActionField
{
    public string Type { get; init; } = "";

    public string Description { get; init; } = "";

    public IReadOnlyList<string>? Values { get; init; }

    public int? Min { get; init; }

    public int? Max { get; init; }

    public int? MaxLength { get; init; }

    // Why: every rule about a type is written once, on the fact's form.
    public GlobalFact Named(string name) =>
        new()
        {
            Name = name,
            Type = Type,
            Description = Description,
            Values = Values,
            Min = Min,
            Max = Max,
            MaxLength = MaxLength,
        };
}
