using Suwagcms.Sdk.Format.Actions;

namespace Suwagcms.Contracts.Actions;

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
