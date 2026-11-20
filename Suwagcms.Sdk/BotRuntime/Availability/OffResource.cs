using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Sdk.BotRuntime.Availability;

public sealed record OffResource(ResourceKind Kind, string Id, int? UpToVersion = null);
