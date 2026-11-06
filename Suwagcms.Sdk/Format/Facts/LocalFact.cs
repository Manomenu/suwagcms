using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format.Facts;

// Why: Type and Description default to empty so that a missing one reaches the rules, which name it with the same code as for a global fact.
public sealed record LocalFact
{
    public string Type { get; init; } = "";

    public string Description { get; init; } = "";

    public string WritableBy { get; init; } = FactWriters.Set;

    public IReadOnlyList<string>? Values { get; init; }

    public int? Min { get; init; }

    public int? Max { get; init; }

    public int? MaxLength { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TextPattern? Pattern { get; init; }

    // Why: a local fact holds exactly what a global one does but its name, so every rule about a fact is written once, on the global form.
    public GlobalFact Named(string name) =>
        new()
        {
            Name = name,
            Type = Type,
            Description = Description,
            WritableBy = WritableBy,
            Values = Values,
            Min = Min,
            Max = Max,
            MaxLength = MaxLength,
            Pattern = Pattern,
        };
}
