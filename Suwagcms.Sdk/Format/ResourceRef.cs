namespace Suwagcms.Sdk.Format;

public sealed record ResourceRef(ResourceKind Kind, string Id)
{
    // Why: a mistake's path names the field a person would type: a fact has a name, everything else an id. A method, so it stays off the wire.
    public string IdField() => Kind == ResourceKind.GlobalFact ? "name" : "id";
}
