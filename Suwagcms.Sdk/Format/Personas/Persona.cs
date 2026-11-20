namespace Suwagcms.Sdk.Format.Personas;

public sealed record Persona
{
    public const string PublicId = "persona";

    public string? Personality { get; init; }

    public PersonaScope? Scope { get; init; }

    public bool HasText() => Texts().Any(text => !string.IsNullOrWhiteSpace(text.Text));

    internal IEnumerable<(string Path, string? Text)> Texts() =>
        [
            ("personality", Personality),
            ("scope.inScope", Scope?.InScope),
            ("scope.outOfScope", Scope?.OutOfScope),
            ("scope.whenOutOfScope", Scope?.WhenOutOfScope),
        ];
}
