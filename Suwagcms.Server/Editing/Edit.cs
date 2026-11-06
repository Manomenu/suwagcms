using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Server.Editing;

// Why: an edit that gives some fields is merged with what is stored under the lock, so no save in between is lost and no stale read is needed.
internal sealed record Edit(ResourceRef Target, Func<object, object> Merge, int? ExpectedRevision) : Change(Target, ExpectedRevision);
