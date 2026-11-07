namespace Suwagcms.Sdk.Format.Declaration;

// Why: what a bot's setup states; there is no default list of blocks, since a default would claim the engine runs something it may not.
public sealed record BotCapabilities
{
    public required int FormatVersion { get; init; }

    public required IReadOnlyList<string> Blocks { get; init; }

    public IReadOnlyList<CodeAction> CodeActions { get; init; } = [];

    // Why: left out, the bot takes a version with or without a persona; BotDeclaration.PersonaRequired has the CMS refuse one without any text, PersonaComplete one with a part left empty.
    public string? Persona { get; init; }

    // Why: left out, the bot does not read an instruction's knowledge: the editor hides the section, and a version holding any is refused.
    public bool Knowledge { get; init; }

    // Why: the names of the embedding profiles the bot's environment sets; a version whose knowledge base names another is refused, since the bot could not embed its articles.
    public IReadOnlyList<string> EmbeddingProfiles { get; init; } = [];

    // Why: the characters one instruction's named articles may hold together, as the bot gives each one whole; left out, there is no limit.
    public int? NamedArticlesLimit { get; init; }
}
