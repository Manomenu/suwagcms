using System.Globalization;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Availability;
using Suwagcms.Server.Bots;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Versions;

// Why: a push reads what it needs, lets go of the database, calls the bot, and only then records; so no lock or transaction ever waits on a bot.
internal sealed class VersionPusher(Pool pool, BotLine line, DeclarationAsker asker, AvailabilitySender availability, TimeProvider time)
{
    public const string NotLatest = "push.notLatest";

    internal sealed record Rejected(int Status, Mistake Mistake);

    public async Task<(PushResult? Result, Rejected? Rejected)> PushAsync(string bot, int number, CancellationToken cancellationToken)
    {
        if (await PushStore.FindBotAsync(pool, bot, cancellationToken) is not { } registered)
        {
            return (null, new Rejected(StatusCodes.Status404NotFound, EditProblems.UnknownBot(bot)));
        }
        if (await VersionStore.FindAsync(pool, bot, number, cancellationToken) is not { } version)
        {
            return (
                null,
                new Rejected(
                    StatusCodes.Status404NotFound,
                    VersionEndpoints.NoSuchVersion(bot, number, await VersionStore.LatestNumberAsync(pool, bot, cancellationToken))
                )
            );
        }
        var latest = await VersionStore.LatestNumberAsync(pool, bot, cancellationToken);
        if (latest != number)
        {
            return (null, new Rejected(StatusCodes.Status422UnprocessableEntity, TooOld(bot, number, latest)));
        }

        var (result, reported) =
            await CannotRunAsync(bot, number, version, cancellationToken) is { } refused ? (refused, false)
            : await AvailabilityMissedAsync(bot, registered.Address, number, cancellationToken) is { } missed ? (missed, false)
            : Judge(bot, registered, number, await line.PutVersionAsync(registered.Address, number, version.Document, cancellationToken));

        // Why: the record must survive a caller that gives up while the bot was answering, since the bot may have taken the version.
        await PushStore.RecordAsync(pool, bot, number, result.Outcome, Recorded(result), time.GetUtcNow(), CancellationToken.None);
        if (reported)
        {
            await PushStore.ReportAsync(pool, bot, result.BotHas, time.GetUtcNow(), CancellationToken.None);
        }
        return (result, null);
    }

    // Why: the whole set goes first, which repairs a missed change, and a new version never runs without it.
    private async Task<PushResult?> AvailabilityMissedAsync(string bot, string address, int number, CancellationToken cancellationToken) =>
        await availability.SendBeforePushAsync(bot, address, cancellationToken)
            is { State: not AvailabilityDeliveryState.Confirmed } delivery
            ? Failed(
                number,
                null,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"what is turned off did not reach the bot, so v{number} was not sent: {delivery.Reason}"
                ),
                []
            )
            : null;

    // Why: a push is judged by what the bot declares at this moment; a bot that gives no good answer is left to its own validation, which the push reaches next.
    private async Task<PushResult?> CannotRunAsync(string bot, int number, VersionDetail version, CancellationToken cancellationToken)
    {
        if (await asker.AskForPushAsync(bot, cancellationToken) is not { Ask: DeclarationAsk.Answered, Declaration: { } declaration })
        {
            return null;
        }
        var mistakes = DefinitionValidator.ValidateJson(version.Document, ValidationStage.Version, declaration);
        return mistakes.Count == 0
            ? null
            : Failed(
                number,
                null,
                string.Create(CultureInfo.InvariantCulture, $"v{number} asks for what the bot does not declare now, so nothing was sent."),
                mistakes
            );
    }

    private static (PushResult Result, bool Reported) Judge(string bot, PushStore.Registered registered, int number, BotLine.Reply reply)
    {
        var version = number.ToString(CultureInfo.InvariantCulture);
        switch (reply)
        {
            case BotLine.Reply.Silent silent:
                return (Failed(number, null, $"{silent.Reason}. {LastKnown(registered)}{CheckBot}", []), false);
            case BotLine.Reply.Answer { Status: >= 200 and < 300 } answer when BotLine.StateIn(answer) is { } state:
                if (state.Bot != bot)
                {
                    return (
                        Failed(
                            number,
                            null,
                            $"the address {registered.Address} answers as bot '{state.Bot}', not '{bot}'. Register the right address for bot '{bot}'.",
                            []
                        ),
                        false
                    );
                }
                if (state.Current == number)
                {
                    return (new PushResult(PushOutcome.Succeeded, number, number, null, []), true);
                }
                return (
                    Failed(
                        number,
                        state.Current,
                        state.Current is { } current
                            ? $"the bot answered but reports v{current} as current, not v{version}. Making a version the bot already holds current again is a rollback, which a push does not do."
                            : $"the bot answered but reports no current version, not v{version}.",
                        []
                    ),
                    true
                );
            case BotLine.Reply.Answer answer when BotLine.RefusalIn(answer) is { } refusal:
                return (Failed(number, null, $"the bot refused v{version}: {refusal.Detail}", refusal.Mistakes), false);
            case BotLine.Reply.Answer answer:
                return (
                    Failed(
                        number,
                        null,
                        $"the bot answered {answer.Status}, which is not the answer of a bot's version route. {CheckBot}",
                        []
                    ),
                    false
                );
            default:
                throw new InvalidOperationException("a bot's reply is an answer or silence");
        }
    }

    private const string CheckBot = "Check that the bot is running and that the CMS can reach it, then push again.";

    private static PushResult Failed(int number, int? botHas, string reason, IReadOnlyList<Mistake> mistakes) =>
        new(PushOutcome.Failed, number, botHas, reason, mistakes);

    private static string LastKnown(PushStore.Registered registered) =>
        registered.ReportedAt is { } at
            ? registered.ReportedVersion is { } number
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"The bot last reported v{number}, at {at.UtcDateTime:yyyy-MM-dd HH:mm} UTC. "
                )
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"The bot last reported no version, at {at.UtcDateTime:yyyy-MM-dd HH:mm} UTC. "
                )
            : "";

    // Why: the page lists attempts as text, so the bot's own reasons are kept with the failure.
    private static string? Recorded(PushResult result) =>
        result.Outcome == PushOutcome.Succeeded
            ? null
            : string.Join(" ", [result.Reason, .. result.Mistakes.Select(mistake => $"{mistake.Message} {mistake.Fix}")]);

    private static Mistake TooOld(string bot, int number, int? latest) =>
        new(
            NotLatest,
            ValidationStage.Save,
            new ResourceRef(ResourceKind.Definition, bot),
            "number",
            string.Create(CultureInfo.InvariantCulture, $"Version {number} of bot '{bot}' is not the latest, which is {latest}."),
            "Push the latest version; going back to an older one is a rollback, which a push does not do.",
            []
        );
}
