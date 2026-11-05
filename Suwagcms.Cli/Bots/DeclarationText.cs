using System.Globalization;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Cli.Bots;

internal static class DeclarationText
{
    private const string CheckBot = "Check that the bot is running and that the CMS can reach it, then ask again.";
    private const string NothingChecked = "Nothing is checked against a declaration until it does.";

    public static string Of(DeclarationStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        var lines = new List<string>();
        switch (status.Ask)
        {
            case DeclarationAsk.Unreachable when status.Stored == DeclarationStored.NeverAnswered:
                lines.Add($"{Capital(status.Reason)}, and it has never answered. {NothingChecked}");
                break;
            case DeclarationAsk.Unreachable:
                lines.Add($"{Capital(status.Reason)}. {LastAnswer(status)}");
                lines.AddRange(Held(status));
                lines.Add(CheckBot);
                break;
            case DeclarationAsk.Invalid:
                lines.Add(
                    string.Create(CultureInfo.InvariantCulture, $"The bot's answer was refused, {status.Mistakes.Count} mistake(s):")
                );
                lines.AddRange(
                    status.Mistakes.Select(mistake =>
                        $"  {mistake.Code} {mistake.Path} {mistake.Message}{Environment.NewLine}    Fix: {mistake.Fix}"
                    )
                );
                lines.Add(
                    status.Stored == DeclarationStored.NeverAnswered
                        ? $"It has never given a good answer. {NothingChecked}"
                        : LastAnswer(status)
                );
                lines.AddRange(Held(status));
                break;
            case DeclarationAsk.Answered:
            case DeclarationAsk.NotAsked:
                lines.AddRange(Answered(status));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), "unknown end of asking a bot");
        }
        if (status.CannotRun.Count > 0)
        {
            lines.Add("Steps using a block the bot does not declare:");
            lines.AddRange(status.CannotRun.Select(mistake => $"  {mistake.Message}"));
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static IEnumerable<string> Answered(DeclarationStatus status) =>
        status.Stored switch
        {
            DeclarationStored.NeverAnswered => [$"Bot {status.Bot} has not answered yet. {NothingChecked}"],
            DeclarationStored.DeclaresNothing => [$"Bot {status.Bot} declares nothing, so nothing is checked against a declaration."],
            DeclarationStored.Declared => Held(status)
                .Prepend(status.Ask == DeclarationAsk.Answered ? $"Bot {status.Bot}, asked just now:" : LastAnswer(status)),
            _ => throw new ArgumentOutOfRangeException(nameof(status), "unknown stored declaration"),
        };

    private static IEnumerable<string> Held(DeclarationStatus status)
    {
        if (status.Stored == DeclarationStored.DeclaresNothing)
        {
            return ["  the bot declares nothing"];
        }
        return status.Declaration is { } declaration ? Lines(declaration, status.FormatFix) : [];
    }

    private static IEnumerable<string> Lines(BotDeclaration declaration, string? formatFix)
    {
        yield return string.Create(CultureInfo.InvariantCulture, $"  format {declaration.FormatVersion}");
        yield return $"  blocks {string.Join(", ", declaration.Blocks)}";
        yield return $"  actions {(declaration.CodeActions.Count == 0 ? "none" : string.Join(", ", declaration.CodeActions.Select(action => action.Id)))}";
        if (declaration.Persona is { } persona)
        {
            yield return $"  persona {persona}";
        }
        if (formatFix is not null)
        {
            yield return formatFix;
        }
    }

    private static string LastAnswer(DeclarationStatus status) =>
        status.AnsweredAt is { } at
            ? string.Create(CultureInfo.InvariantCulture, $"Last answer, from {at.UtcDateTime:yyyy-MM-dd HH:mm} UTC:")
            : "No answer yet.";

    private static string Capital(string? reason) =>
        string.IsNullOrEmpty(reason) ? "No answer" : char.ToUpperInvariant(reason[0]) + reason[1..];
}
