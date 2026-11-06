namespace Suwagcms.Sdk.Format.Declaration;

// Why: what a bot's setup states; there is no default list of blocks, since a default would claim the engine runs something it may not.
public sealed record BotCapabilities
{
    public required int FormatVersion { get; init; }

    public required IReadOnlyList<string> Blocks { get; init; }

    public IReadOnlyList<CodeAction> CodeActions { get; init; } = [];

    // Why: left out, the bot takes a version with or without a persona; BotDeclaration.PersonaRequired has the CMS refuse one without any text, PersonaComplete one with a part left empty.
    public string? Persona { get; init; }
}
