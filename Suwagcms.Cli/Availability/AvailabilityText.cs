using Suwagcms.Contracts.Availability;
using Suwagcms.Contracts.Usage;

namespace Suwagcms.Cli.Availability;

internal static class AvailabilityText
{
    private const string SendHint = "The bot still has the previous setting. Send it with: suwagcms availability send";

    public static string UsedBy(IReadOnlyList<UsageLink> usedBy) =>
        string.Join(Environment.NewLine, ["Used by:", .. usedBy.Select(link => $"  {link.Kind} {link.Id}, {link.Field}")]);

    public static string NeedsYes(AvailabilityAnswer answer) =>
        string.Join(
            Environment.NewLine,
            [UsedBy(answer.UsedBy), $"Nothing changed. Repeat with --yes to turn {answer.Kind} {answer.Id} off."]
        );

    public static string DryRun(AvailabilityAnswer answer)
    {
        var word = answer.Off ? "off" : "on";
        return string.Join(
            Environment.NewLine,
            [
                .. answer.UsedBy.Count == 0 ? (answer.Off ? ["Nothing uses it."] : Array.Empty<string>()) : [UsedBy(answer.UsedBy)],
                answer.Changed ? $"Would turn {answer.Kind} {answer.Id} {word}." : $"{answer.Kind} {answer.Id} is already {word}.",
                "Nothing was changed.",
            ]
        );
    }

    public static string Done(string bot, AvailabilityAnswer answer)
    {
        var word = answer.Off ? "off" : "on";
        var head = answer.UsedBy.Count == 0 ? Array.Empty<string>() : [UsedBy(answer.UsedBy)];
        return string.Join(
            Environment.NewLine,
            answer.Delivery.State == AvailabilityDeliveryState.Confirmed
                ?
                [
                    .. head,
                    $"{(answer.Changed ? $"Turned {word}." : $"{answer.Kind} {answer.Id} is already {word}, and was sent again.")} Bot {bot} confirmed.",
                ]
                :
                [
                    .. head,
                    $"{(answer.Changed ? $"Turned {word} in the CMS" : $"{answer.Kind} {answer.Id} is already {word} in the CMS")}, not delivered: {answer.Delivery.Reason}",
                    SendHint,
                ]
        );
    }

    public static string List(string bot, AvailabilityView view, bool stored) =>
        string.Join(
            Environment.NewLine,
            [
                .. view.Resources.Select(item => $"{item.Kind} {item.Id}: {(item.Off ? "off" : "on")}"),
                stored ? $"Bot {bot} was not asked: this is what the CMS holds." : Delivery(bot, view.Delivery),
            ]
        );

    public static string Delivery(string bot, AvailabilityDelivery delivery) =>
        delivery.State switch
        {
            AvailabilityDeliveryState.Confirmed => $"Bot {bot} confirmed.",
            AvailabilityDeliveryState.NotAsked => $"Nothing has been turned off for bot {bot}, so it was not asked.",
            AvailabilityDeliveryState.NotDelivered => $"Not delivered to bot {bot}: {delivery.Reason}",
            _ => throw new ArgumentOutOfRangeException(nameof(delivery)),
        };
}
