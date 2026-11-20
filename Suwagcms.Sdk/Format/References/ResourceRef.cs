namespace Suwagcms.Sdk.Format.References;

public sealed record ResourceRef(ResourceKind Kind, string Id)
{
    public string IdField() => Kind == ResourceKind.GlobalFact ? "name" : "id";
}
