using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Server.Editing;

// Why: a change of its own, not a remove and a put: the row keeps what hangs on it and every referrer is rewritten under the same lock, so a link saved at that moment is either rewritten or refused.
internal sealed record Rename(ResourceRef Target, string NewId, int? ExpectedRevision) : Change(Target, ExpectedRevision)
{
    public ResourceRef NewTarget => new(Target.Kind, NewId);
}
