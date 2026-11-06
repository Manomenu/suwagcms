using System.Text.Json.Serialization;
using Suwagcms.Sdk.Format.Facts;

namespace Suwagcms.Sdk.Format.Procedures;

public sealed record Condition
{
    public IReadOnlyList<string>? OneOf { get; init; }

    public NumberRange? Range { get; init; }

    public bool? Known { get; init; }

    [JsonIgnore]
    public IReadOnlyList<string> Kinds =>
        [.. new[] { OneOf is null ? null : "oneOf", Range is null ? null : "range", Known is null ? null : "known" }.OfType<string>()];
}
