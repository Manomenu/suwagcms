using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Server.Editing;

internal sealed record StoredResource(Guid Id, ResourceRef Target, int Revision, object Record);
