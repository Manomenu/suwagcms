using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts.Changes;

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
