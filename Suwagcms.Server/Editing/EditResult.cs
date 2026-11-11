using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Server.Editing;

internal abstract record EditResult
{
    // Why: Revisions holds the revision each put resource has (or would have, on a dry run); a removed resource has none. NotReady is what still stops a version, which never refuses a save.
    internal sealed record Saved(IReadOnlyDictionary<ResourceRef, int> Revisions, bool Written, IReadOnlyList<Mistake> NotReady)
        : EditResult
    {
        // Why: what a rename rewrote, found under the lock, as the state before it names it.
        public IReadOnlyList<Reference> Rewritten { get; init; } = [];

        // Why: the state found under the lock and the one the save leaves, so a caller can tell an add from a replacement and from one that changed nothing, and say what differs.
        public IReadOnlyDictionary<ResourceRef, StoredResource> Before { get; init; } = new Dictionary<ResourceRef, StoredResource>();

        public IReadOnlyDictionary<ResourceRef, StoredResource> After { get; init; } = new Dictionary<ResourceRef, StoredResource>();
    }

    internal sealed record Refused(IReadOnlyList<Mistake> Mistakes) : EditResult;

    // Why: Current is null when the resource does not exist, which is how an edit of a deleted resource looks.
    internal sealed record Stale(ResourceRef Target, int Expected, int? Current) : EditResult;

    internal sealed record Missing(ResourceRef Target) : EditResult;

    internal sealed record UnknownBot : EditResult;
}
