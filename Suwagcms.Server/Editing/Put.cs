using Suwagcms.Sdk.Format;

namespace Suwagcms.Server.Editing;

internal sealed record Put(ResourceRef Target, object Record, int? ExpectedRevision) : Change(Target, ExpectedRevision);
