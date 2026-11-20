using System.Globalization;
using Npgsql;
using Suwagcms.Contracts.Availability;
using Suwagcms.Sdk.BotRuntime;
using Suwagcms.Sdk.BotRuntime.Availability;
using Suwagcms.Server.Bots;

namespace Suwagcms.Server.Availability;

internal sealed class AvailabilitySender(NpgsqlDataSource dataSource, BotClient client, AvailabilityRetries retries)
{
    private const string SendAgain = "Check that the bot is running, then send again.";

    public async Task<AvailabilityDelivery> SendAsync(string bot, string address, CancellationToken cancellationToken)
    {
        var message = await AvailabilityStore.ReadMessageAsync(dataSource, bot, cancellationToken);
        return await DeliverAsync(bot, address, message, cancellationToken);
    }

    public async Task<AvailabilityDelivery?> CatchUpAsync(string bot, string address, CancellationToken cancellationToken)
    {
        var message = await AvailabilityStore.ReadMessageAsync(dataSource, bot, cancellationToken);
        return message.Number == 0 ? null : await DeliverAsync(bot, address, message, cancellationToken);
    }

    public async Task<AvailabilityDelivery> AskAsync(string bot, string address, CancellationToken cancellationToken)
    {
        var number = await AvailabilityStore.ReadNumberAsync(dataSource, bot, cancellationToken);
        return number == 0
            ? new AvailabilityDelivery(AvailabilityDeliveryState.NotAsked, number, null, null)
            : Judge(bot, address, number, await client.ReadStateAsync(address, cancellationToken));
    }

    private async Task<AvailabilityDelivery> DeliverAsync(
        string bot,
        string address,
        BotAvailability message,
        CancellationToken cancellationToken
    )
    {
        var reply = await client.PutAvailabilityAsync(address, message, cancellationToken);
        var delivery = Judge(bot, address, await AvailabilityStore.ReadNumberAsync(dataSource, bot, cancellationToken), reply);
        if (delivery.State == AvailabilityDeliveryState.Confirmed)
        {
            retries.Cancel(bot);
        }
        else
        {
            retries.Schedule(bot);
        }
        return delivery;
    }

    private static AvailabilityDelivery Judge(string bot, string address, int number, BotClient.Reply reply) =>
        reply switch
        {
            BotClient.Reply.Silent silent => NotDelivered(number, null, $"{silent.Reason}. {SendAgain}"),
            BotClient.Reply.Answer { Status: StatusCodes.Status404NotFound } => NotDelivered(
                number,
                null,
                $"{address} has no availability route: update Suwagcms.Sdk in the bot, then send again."
            ),
            BotClient.Reply.Answer { Status: >= 200 and < 300 } answer when BotClient.StateIn(answer) is { } state => Reported(
                bot,
                address,
                number,
                state
            ),
            BotClient.Reply.Answer answer when BotClient.RefusalIn(answer) is { } refusal => NotDelivered(
                number,
                null,
                $"the bot refused it: {refusal.Detail} {SendAgain}"
            ),
            BotClient.Reply.Answer answer => NotDelivered(
                number,
                null,
                string.Create(CultureInfo.InvariantCulture, $"{address} answered {answer.Status}, which is not a bot's answer. {SendAgain}")
            ),
            _ => throw new InvalidOperationException("a bot's reply is an answer or silence"),
        };

    private static AvailabilityDelivery Reported(string bot, string address, int number, BotState state)
    {
        var has = state.Availability.Number;
        if (state.Bot != bot)
        {
            return NotDelivered(
                number,
                null,
                $"the address {address} answers as bot '{state.Bot}', not '{bot}'. Register the right address."
            );
        }
        return has == number ? new AvailabilityDelivery(AvailabilityDeliveryState.Confirmed, number, has, null)
            : has < number
                ? NotDelivered(
                    number,
                    has,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"the bot reports number {has}, lower than the CMS's {number}, so it does not have the latest setting. Send again."
                    )
                )
            : NotDelivered(
                number,
                has,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"the bot reports number {has}, higher than the CMS's {number}, so it ignores what this CMS sends. Check that the CMS's database and the bot's store belong together."
                )
            );
    }

    private static AvailabilityDelivery NotDelivered(int number, int? botNumber, string reason) =>
        new(AvailabilityDeliveryState.NotDelivered, number, botNumber, reason);
}
