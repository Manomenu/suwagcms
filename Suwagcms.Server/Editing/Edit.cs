using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Server.Editing;

internal sealed record Edit(ResourceRef Target, Func<object, object> Merge, int? ExpectedRevision) : Change(Target, ExpectedRevision);
