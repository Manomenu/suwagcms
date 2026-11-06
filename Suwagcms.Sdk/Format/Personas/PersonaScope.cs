namespace Suwagcms.Sdk.Format.Personas;

public sealed record PersonaScope
{
    public string? InScope { get; init; }

    public string? OutOfScope { get; init; }

    public string? WhenOutOfScope { get; init; }
}
