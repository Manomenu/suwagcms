using Suwagcms.Sdk.Format.Facts;

namespace Suwagcms.Contracts.Facts;

/// <summary>What saving a fact takes: every field of it but the name, which the address gives. A field left out is checked by the server; only writableBy has a default, set.</summary>
public sealed record SaveFact(
    string? Type,
    string? Description,
    string? WritableBy,
    IReadOnlyList<string>? Values,
    int? Min,
    int? Max,
    int? MaxLength,
    TextPattern? Pattern
);
