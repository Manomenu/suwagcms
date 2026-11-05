namespace Suwagcms.Sdk.Format;

// Why: what a bot's setup states; there is no default list of blocks, since a default would claim the engine runs something it may not.
public sealed record BotCapabilities
{
    public required int FormatVersion { get; init; }

    public required IReadOnlyList<string> Blocks { get; init; }

    public IReadOnlyList<CodeAction> CodeActions { get; init; } = [];
}
