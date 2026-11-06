namespace Suwagcms.Sdk.Format.Personas;

// Why: each text is one string kept as written, so a line break is part of the text and nothing here reads or splits it.
public sealed record Persona
{
    // Why: a bot has one persona, so the id is fixed: it is how the one resource table and the rules name it, never a choice.
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
