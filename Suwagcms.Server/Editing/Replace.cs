using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Server.Editing;

internal sealed record Replace(ResourceRef Target, object Record, int? ExpectedRevision) : Change(Target, ExpectedRevision);
