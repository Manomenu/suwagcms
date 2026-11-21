using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;
using Suwbackkit.Server;

namespace Suwagcms.Server.Changes;

internal sealed record ChangeMistake(
    int? Change,
    string Code,
    string Message,
    string Fix,
    ValidationStage Stage,
    ResourceRef Resource,
    IReadOnlyList<RelatedResource> Related
) : Mistake<string>(Code, Message, Fix);
