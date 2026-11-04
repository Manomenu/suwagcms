using Suwagcms.Contracts;

namespace Suwagcms.Cli.Bots;

internal sealed record BotListDocument(IReadOnlyList<Bot> Bots, string? Active);
