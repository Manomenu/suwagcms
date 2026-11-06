using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format.Declaration;

// Why: the answer of GET /suwagcms/declaration and an input of the validator; every field has a default so that a bad answer lists all its mistakes instead of stopping at the first.
public sealed record BotDeclaration
{
    public const string PersonaRequired = "required";

    public const string PersonaComplete = "complete";

    public string Bot { get; init; } = "";

    public bool DeclaresNothing { get; init; }

    public int? FormatVersion { get; init; }

    public IReadOnlyList<string> Blocks { get; init; } = [];

    public IReadOnlyList<CodeAction> CodeActions { get; init; } = [];

    public string? Persona { get; init; }

    // Why: left out when false, so a bot that says nothing about knowledge answers as it always did.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Knowledge { get; init; }

    public static BotDeclaration Of(string bot, BotCapabilities? capabilities) =>
        capabilities is null
            ? new BotDeclaration { Bot = bot, DeclaresNothing = true }
            : new BotDeclaration
            {
                Bot = bot,
                FormatVersion = capabilities.FormatVersion,
                Blocks = capabilities.Blocks,
                CodeActions = capabilities.CodeActions,
                Persona = capabilities.Persona,
                Knowledge = capabilities.Knowledge,
            };
}
