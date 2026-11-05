using Suwagcms.Sdk.Format;

namespace Suwagcms.Contracts;

/// <summary>What saving an action takes: every field of it but the id, which the address gives. A field left out is checked by the server, which reports every mistake at once. A secret stands in a header as {secret:NAME}, never as its value.</summary>
public sealed record SaveAction(
    string? Description,
    IReadOnlyDictionary<string, ActionField>? Parameters,
    IReadOnlyDictionary<string, ActionField>? Results,
    ActionImplementation? Implementation
);
