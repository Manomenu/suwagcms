using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format.Facts;

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
