using System.Globalization;
using Suwagcms.Contracts.Availability;
using Suwagcms.Contracts.Bots;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.BotRuntime;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Availability;
using Suwagcms.Server.Bots;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Versions;

internal sealed class VersionPusher(Pool pool, BotClient client, DeclarationAsker asker, AvailabilitySender availability, TimeProvider time)
{
    internal sealed record Rejected(int Status, Mistake Mistake);

    private sealed record Target(PushStore.Registered Registered, VersionDetail Version);

    private sealed record Route(PushWay Way, int? Left, PushResult? Refused);

    public async Task<(PushResult? Result, Rejected? Rejected)> PushAsync(string bot, int number, CancellationToken cancellationToken)
    {
        var (target, rejected) = await FindAsync(bot, number, cancellationToken);
        if (target is null)
        {
            return (null, rejected);
        }

        var route = await ChooseAsync(bot, number, target, cancellationToken);
        var (result, reported) = route switch
        {
            { Refused: { } refused } => (refused, false),
            { Way: PushWay.Nothing } => (new PushResult(PushOutcome.Succeeded, number, number, null, []), true),
            _ => await DeliverAsync(bot, number, target, route.Way, cancellationToken),
        };

        await PushStore.RecordAsync(
            pool,
            bot,
            number,
            route.Left,
            result.Outcome,
            Recorded(result),
            time.GetUtcNow(),
            CancellationToken.None
        );
        if (reported)
        {
            await PushStore.ReportAsync(pool, bot, result.BotHas, time.GetUtcNow(), CancellationToken.None);
        }
        return (result, null);
    }

    public async Task<(PushPlan? Plan, Rejected? Rejected)> PlanAsync(string bot, int number, CancellationToken cancellationToken)
    {
        var (target, rejected) = await FindAsync(bot, number, cancellationToken);
        if (target is null)
        {
            return (null, rejected);
        }

        var route = await ChooseAsync(bot, number, target, cancellationToken);
        return (
            new PushPlan(
                number,
                route.Left,
                route.Way,
                route.Refused?.Reason,
                route.Refused?.Mistakes ?? [],
                await ChangesAsync(bot, route.Left, target.Version, number, cancellationToken)
            ),
            null
        );
    }

    private async Task<(Target? Target, Rejected? Rejected)> FindAsync(string bot, int number, CancellationToken cancellationToken)
    {
        if (await PushStore.FindBotAsync(pool, bot, cancellationToken) is not { } registered)
        {
            return (null, new Rejected(StatusCodes.Status404NotFound, EditProblems.UnknownBot(bot)));
        }
        return await VersionStore.FindAsync(pool, bot, number, cancellationToken) is { } version
            ? (new Target(registered, version), null)
            : (null, new Rejected(StatusCodes.Status404NotFound, await NoSuchVersionAsync(bot, number, cancellationToken)));
    }

    private async Task<Mistake> NoSuchVersionAsync(string bot, int number, CancellationToken cancellationToken)
    {
        var kept = (await VersionStore.ListAsync(pool, bot, cancellationToken) ?? []).Select(version => version.Number).ToList();
        var missing = VersionEndpoints.NoSuchVersion(bot, number, kept.Count == 0 ? null : kept.Max());
        if (kept.Count == 0)
        {
            return missing;
        }
        var nearest = kept.OrderBy(version => Math.Abs((long)version - number)).ThenByDescending(version => version).Take(3).Order();
        return missing with
        {
            Message =
                $"Bot '{bot}' has no version {number}; the nearest it keeps: {string.Join(", ", nearest.Select(version => $"v{version}"))}.",
        };
    }

    private async Task<Route> ChooseAsync(string bot, int number, Target target, CancellationToken cancellationToken)
    {
        var registered = target.Registered;
        var reply = await client.ReadStateAsync(registered.Address, cancellationToken);
        if (reply is BotClient.Reply.Silent)
        {
            return new Route(PushWay.Send, null, Judge(bot, registered, number, reply).Result);
        }
        var state = reply is BotClient.Reply.Answer { Status: StatusCodes.Status200OK } answer ? BotClient.StateIn(answer) : null;
        if (state is not null && state.Bot != bot)
        {
            return new Route(PushWay.Send, null, WrongAddress(bot, registered, number, state));
        }

        if (state?.Current == number)
        {
            return new Route(PushWay.Nothing, number, null);
        }
        var way = state?.Held.Contains(number) == true ? PushWay.MakeCurrent : PushWay.Send;
        return new Route(way, state?.Current, await CannotRunAsync(bot, number, target.Version, cancellationToken));
    }

    private async Task<(PushResult Result, bool Reported)> DeliverAsync(
        string bot,
        int number,
        Target target,
        PushWay way,
        CancellationToken cancellationToken
    )
    {
        if (await AvailabilityMissedAsync(bot, target.Registered.Address, number, cancellationToken) is { } missed)
        {
            return (missed, false);
        }
        return way == PushWay.MakeCurrent
            ? await MakeCurrentAsync(bot, number, target, cancellationToken)
            : await SendAsync(bot, number, target, cancellationToken);
    }

    private async Task<(PushResult Result, bool Reported)> SendAsync(
        string bot,
        int number,
        Target target,
        CancellationToken cancellationToken
    ) =>
        Judge(
            bot,
            target.Registered,
            number,
            await client.PutVersionAsync(target.Registered.Address, number, target.Version.Document, cancellationToken)
        );

    private async Task<(PushResult Result, bool Reported)> MakeCurrentAsync(
        string bot,
        int number,
        Target target,
        CancellationToken cancellationToken
    )
    {
        var reply = await client.PutCurrentAsync(target.Registered.Address, number, target.Version.Version.Sha256, cancellationToken);
        return reply switch
        {
            BotClient.Reply.Answer { Status: StatusCodes.Status409Conflict } answer
                when BotClient.RefusalIn(answer)?.Mistakes.Any(mistake => mistake.Code == MistakeCodes.VersionNotHeld) == true =>
                await SendAsync(bot, number, target, cancellationToken),
            BotClient.Reply.Answer
            {
                Status: StatusCodes.Status404NotFound or StatusCodes.Status405MethodNotAllowed or StatusCodes.Status501NotImplemented,
            } => (
                Failed(
                    number,
                    null,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"the bot holds v{number} but has no route to make a held version current. Upgrade the bot's Suwagcms.Sdk, then push again; a new version also reaches it as it is."
                    ),
                    []
                ),
                false
            ),
            _ => Judge(bot, target.Registered, number, reply),
        };
    }

    private async Task<VersionComparison?> ChangesAsync(
        string bot,
        int? left,
        VersionDetail version,
        int number,
        CancellationToken cancellationToken
    ) =>
        left is { } from && from != number && await VersionStore.FindAsync(pool, bot, from, cancellationToken) is { } before
            ? VersionComparer.Compare(bot, from, number, before.Document, version.Document)
            : null;

    private async Task<PushResult?> AvailabilityMissedAsync(string bot, string address, int number, CancellationToken cancellationToken) =>
        await availability.CatchUpAsync(bot, address, cancellationToken) is { State: not AvailabilityDeliveryState.Confirmed } delivery
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

    private static (PushResult Result, bool Reported) Judge(string bot, PushStore.Registered registered, int number, BotClient.Reply reply)
    {
        var version = number.ToString(CultureInfo.InvariantCulture);
        switch (reply)
        {
            case BotClient.Reply.Silent silent:
                return (Failed(number, null, $"{silent.Reason}. {LastKnown(registered)}{CheckBot}", []), false);
            case BotClient.Reply.Answer { Status: >= 200 and < 300 } answer when BotClient.StateIn(answer) is { } state:
                if (state.Bot != bot)
                {
                    return (WrongAddress(bot, registered, number, state), false);
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
                            ? $"the bot answered but reports v{current} as current, not v{version}."
                            : $"the bot answered but reports no current version, not v{version}.",
                        []
                    ),
                    true
                );
            case BotClient.Reply.Answer answer when BotClient.RefusalIn(answer) is { } refusal:
                return (Failed(number, null, $"the bot refused v{version}: {refusal.Detail}", refusal.Mistakes), false);
            case BotClient.Reply.Answer answer:
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

    private static PushResult WrongAddress(string bot, PushStore.Registered registered, int number, BotState state) =>
        Failed(
            number,
            null,
            $"the address {registered.Address} answers as bot '{state.Bot}', not '{bot}'. Register the right address for bot '{bot}'.",
            []
        );

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

    private static string? Recorded(PushResult result) =>
        result.Outcome == PushOutcome.Succeeded
            ? null
            : string.Join(" ", [result.Reason, .. result.Mistakes.Select(mistake => $"{mistake.Message} {mistake.Fix}")]);
}
