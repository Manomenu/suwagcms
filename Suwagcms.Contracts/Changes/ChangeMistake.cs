using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts.Changes;

/// <summary>A mistake as a save returns it, with the position in the file of the change it belongs to, from 1. Change is empty when the mistake is on the file as a whole or on a resource the file does not name.</summary>
public sealed record ChangeMistake(
    int? Change,
    string Code,
    ValidationStage Stage,
    ResourceRef Resource,
    string Path,
    string Message,
    string Fix,
    IReadOnlyList<RelatedResource> Related
);
