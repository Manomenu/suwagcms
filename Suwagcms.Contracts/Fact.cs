using Suwagcms.Sdk.Format;

namespace Suwagcms.Contracts;

/// <summary>A global fact of a bot as the API shows it: the format's fields, the revision to send back to guard an edit, and the procedures that use it.</summary>
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
