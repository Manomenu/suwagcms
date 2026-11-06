using Suwagcms.Contracts.Bots;

namespace Suwagcms.Cli.Bots;

internal sealed record BotListDocument(IReadOnlyList<Bot> Bots, string? Active);
