using Suwagcms.Sdk.Format.Actions;

namespace Suwagcms.Contracts.Actions;

public sealed record SaveAction(
    string? Description,
    IReadOnlyDictionary<string, ActionField>? Parameters,
    IReadOnlyDictionary<string, ActionField>? Results,
    ActionImplementation? Implementation
);
