using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Server.Editing;

internal sealed record Remove(ResourceRef Target, int? ExpectedRevision) : Change(Target, ExpectedRevision);
