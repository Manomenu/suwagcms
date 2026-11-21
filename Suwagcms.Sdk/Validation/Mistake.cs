using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Sdk.Validation;

public sealed record Mistake(
    MistakeCode Code,
    ValidationStage Stage,
    ResourceRef Resource,
    string Path,
    string Message,
    string Fix,
    IReadOnlyList<RelatedResource> Related
);
