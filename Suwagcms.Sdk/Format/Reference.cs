namespace Suwagcms.Sdk.Format;

public sealed record Reference(ResourceRef From, string Path, ResourceRef To, FactUse Use);
