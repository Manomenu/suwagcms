using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Server.Editing;

// Why: a put that may find the resource there or not, decided under the lock; one that would store what is already stored is no edit, so it leaves the revision and the history as they are.
internal sealed record Replace(ResourceRef Target, object Record, int? ExpectedRevision) : Change(Target, ExpectedRevision);
