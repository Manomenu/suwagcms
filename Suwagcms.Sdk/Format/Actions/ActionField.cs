using Suwagcms.Sdk.Format.Facts;

namespace Suwagcms.Sdk.Format.Actions;

public sealed record ActionField
{
    public string Type { get; init; } = "";

    public string Description { get; init; } = "";

    public IReadOnlyList<string>? Values { get; init; }

    public int? Min { get; init; }

    public int? Max { get; init; }

    public int? MaxLength { get; init; }

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
