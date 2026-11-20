using Suwagcms.Sdk.Format.Facts;

namespace Suwagcms.Sdk.Format.References;

public sealed record Reference(ResourceRef From, string Path, ResourceRef To, FactUse? Use);
