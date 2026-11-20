using Suwagcms.Sdk.Format.Facts;

namespace Suwagcms.Contracts.Facts;

public sealed record Fact(
    string Name,
    string Type,
    string Description,
    string WritableBy,
    IReadOnlyList<string>? Values,
    int? Min,
    int? Max,
    int? MaxLength,
    TextPattern? Pattern,
    int Revision,
    IReadOnlyList<string> UsedBy
);
