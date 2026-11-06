using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Sdk.BotRuntime.Availability;

// Why: a version keeps the id a resource had when it was created, so a former id counts only up to the last version that can hold it; another resource may take that id afterwards.
public sealed record OffResource(ResourceKind Kind, string Id, int? UpToVersion = null);
