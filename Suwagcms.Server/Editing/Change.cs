using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Server.Editing;

internal abstract record Change(ResourceRef Target, int? ExpectedRevision);
