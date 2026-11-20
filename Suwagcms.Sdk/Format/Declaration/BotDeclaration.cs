using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format.Declaration;

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

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Knowledge { get; init; }

    public IReadOnlyList<string> EmbeddingProfiles { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NamedArticlesLimit { get; init; }

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
                EmbeddingProfiles = capabilities.EmbeddingProfiles,
                NamedArticlesLimit = capabilities.NamedArticlesLimit,
            };
}
