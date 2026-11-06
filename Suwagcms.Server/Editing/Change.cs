using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Server.Editing;

// Why: ExpectedRevision null applies to the latest revision, 0 means the resource must not exist yet, and a number means "the one I read".
internal abstract record Change(ResourceRef Target, int? ExpectedRevision);
