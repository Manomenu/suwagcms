using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;
using Suwbackkit.Server;

namespace Suwagcms.Server.Editing;

internal sealed record RefusalMistake(
    string Code,
    string Message,
    string Fix,
    ValidationStage Stage,
    ResourceRef Resource,
    IReadOnlyList<RelatedResource> Related
) : Mistake<string>(Code, Message, Fix)
{
    public static RefusalMistake Of(Mistake mistake) =>
        new(mistake.Code, mistake.Message, mistake.Fix, mistake.Stage, mistake.Resource, mistake.Related) { At = mistake.Path };
}
