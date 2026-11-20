using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts.Bots;

public sealed record DeclarationStatus(
    string Bot,
    string Address,
    DeclarationAsk Ask,
    DeclarationStored Stored,
    BotDeclaration? Declaration,
    DateTimeOffset? AnsweredAt,
    string? Reason,
    IReadOnlyList<Mistake> Mistakes,
    string? FormatFix,
    IReadOnlyList<Mistake> CannotRun
);
