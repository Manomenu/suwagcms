using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Server.Editing;

internal sealed record Put(ResourceRef Target, object Record, int? ExpectedRevision) : Change(Target, ExpectedRevision);
