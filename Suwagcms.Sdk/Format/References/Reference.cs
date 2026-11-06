using Suwagcms.Sdk.Format.Facts;

namespace Suwagcms.Sdk.Format.References;

// Why: Use says how a procedure uses a fact; a link between texts has none.
public sealed record Reference(ResourceRef From, string Path, ResourceRef To, FactUse? Use);
