using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Server.Editing;

internal abstract record EditResult
{
    internal sealed record Saved(IReadOnlyDictionary<ResourceRef, int> Revisions, bool Written, IReadOnlyList<Mistake> NotReady)
        : EditResult
    {
        public IReadOnlyList<Reference> Rewritten { get; init; } = [];

        public IReadOnlyDictionary<ResourceRef, StoredResource> Before { get; init; } = new Dictionary<ResourceRef, StoredResource>();

        public IReadOnlyDictionary<ResourceRef, StoredResource> After { get; init; } = new Dictionary<ResourceRef, StoredResource>();
    }

    internal sealed record Refused(IReadOnlyList<Mistake> Mistakes) : EditResult;

    internal sealed record Stale(ResourceRef Target, int Expected, int? Current) : EditResult;

    internal sealed record Missing(ResourceRef Target) : EditResult;

    internal sealed record UnknownBot : EditResult;
}
