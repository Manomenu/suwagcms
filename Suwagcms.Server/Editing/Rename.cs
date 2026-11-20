using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Server.Editing;

internal sealed record Rename(ResourceRef Target, string NewId, int? ExpectedRevision) : Change(Target, ExpectedRevision)
{
    public ResourceRef NewTarget => new(Target.Kind, NewId);
}
