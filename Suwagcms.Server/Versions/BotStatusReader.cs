using Suwagcms.Contracts.Bots;
using Suwagcms.Server.Availability;
using Suwagcms.Server.Bots;
using Suwagcms.Server.Database;

namespace Suwagcms.Server.Versions;

// Why: what a bot "has" comes only from the bot's own answer, so this is the single place that turns an answer, or its absence, into a status.
internal sealed class BotStatusReader(Pool pool, BotClient client, TimeProvider time, AvailabilitySender availability)
{
    private const int Recent = 10;

    public async Task<BotStatus?> ReadAsync(string bot, CancellationToken cancellationToken)
    {
        if (await PushStore.FindBotAsync(pool, bot, cancellationToken) is not { } registered)
        {
            return null;
        }
        var latest = await VersionStore.LatestNumberAsync(pool, bot, cancellationToken);
        var attempts = await PushStore.RecentAsync(pool, bot, Recent, cancellationToken);
        var known = new BotStatus(
            bot,
            registered.Address,
            BotReach.Unreachable,
            null,
            [],
            [],
            registered.ReportedVersion,
            registered.ReportedAt,
            null,
            null,
            latest,
            false,
            attempts
        );

        switch (await client.ReadStateAsync(registered.Address, cancellationToken))
        {
            case BotClient.Reply.Answer { Status: 200 } answer when BotClient.StateIn(answer) is { } state:
                if (state.Bot != bot)
                {
                    return known with { Reach = BotReach.WrongAddress, OtherBot = state.Bot };
                }
                await PushStore.ReportAsync(pool, bot, state.Current, time.GetUtcNow(), cancellationToken);
                // Why: a bot that was down or restarting when something was turned off answers here with a lower number; the whole set with the latest number is safe to send again, and the availability screens show how it went.
                if (state.Availability.Number < await AvailabilityStore.ReadNumberAsync(pool, bot, cancellationToken))
                {
                    await availability.CatchUpAsync(bot, registered.Address, cancellationToken);
                }
                return known with
                {
                    Reach = BotReach.Answered,
                    Has = state.Current,
                    Held = [.. state.Held],
                    InUse = [.. state.InUse],
                    LastKnown = state.Current,
                    LastKnownAt = time.GetUtcNow(),
                    Reason = state.StoreError,
                    HasUnknownVersion =
                        state.Current is { } current && !await VersionStore.ExistsAsync(pool, bot, current, cancellationToken),
                };
            case BotClient.Reply.Answer answer:
                return known with
                {
                    Reason = $"{registered.Address} answered {answer.Status}, which is not the answer of a bot's state route",
                };
            case BotClient.Reply.Silent silent:
                return known with { Reason = silent.Reason };
            default:
                throw new InvalidOperationException("a bot's reply is an answer or silence");
        }
    }
}
