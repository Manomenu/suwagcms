using Suwagcms.Sdk.Format.Actions;

namespace Suwagcms.Contracts.Actions;

/// <summary>An action of a bot as the API shows it: the format's fields, with secrets only as the names its headers hold, the revision to send back to guard an edit, the procedures that use it, whether the bot also declares this id as a code action, and whether a code action in use is no longer declared.</summary>
public sealed record ActionView(
    string Id,
    string Description,
    IReadOnlyDictionary<string, ActionField>? Parameters,
    IReadOnlyDictionary<string, ActionField>? Results,
    ActionImplementation? Implementation,
    int Revision,
    IReadOnlyList<string> UsedBy,
    bool AlsoDeclared,
    bool NoLongerDeclared
);
