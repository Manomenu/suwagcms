using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Contracts;

/// <summary>
/// What a bot last declared and what this look at it came to. <c>Declaration</c> and <c>AnsweredAt</c> are the last good answer;
/// <c>Reason</c> says why the bot gave none now; <c>Mistakes</c> lists what is wrong with a refused answer;
/// <c>FormatFix</c> says what to do about a format version this CMS does not read; <c>CannotRun</c> lists the steps of the working state using a block the declaration leaves out.
/// </summary>
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
