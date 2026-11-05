using Suwagcms.Sdk.Format;

namespace Suwagcms.Contracts;

/// <summary>An action of a bot as the API shows it: the format's fields, with secrets only as the names its headers hold, the revision to send back to guard an edit, and the procedures that use it.</summary>
public sealed record ActionView(
    string Id,
    string Description,
    IReadOnlyDictionary<string, ActionField>? Parameters,
    IReadOnlyDictionary<string, ActionField>? Results,
    ActionImplementation? Implementation,
    int Revision,
    IReadOnlyList<string> UsedBy
);
