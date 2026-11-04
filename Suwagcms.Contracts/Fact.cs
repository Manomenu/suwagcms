namespace Suwagcms.Contracts;

/// <summary>A global fact of a bot as the API shows it: the format's fields, and the revision to send back to guard an edit.</summary>
public sealed record Fact(
    string Name,
    string Type,
    string Description,
    string WritableBy,
    IReadOnlyList<string>? Values,
    int? Min,
    int? Max,
    int? MaxLength,
    int Revision
);
