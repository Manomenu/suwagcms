using System.Text.Json;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.BotRuntime.Availability;

internal static class AvailabilityIntake
{
    private static readonly ResourceKind[] CanBeOff = [ResourceKind.Article, ResourceKind.Procedure, ResourceKind.Action];

    public static (BotAvailability? Message, IReadOnlyList<Mistake> Mistakes) Read(string json)
    {
        BotAvailability? message;
        try
        {
            message = JsonSerializer.Deserialize<BotAvailability>(json, BotRuntimeJson.Options);
        }
        catch (JsonException exception)
        {
            return (null, [Invalid("$", $"The body is not an availability message: {exception.Message}")]);
        }
        if (message?.Off is null)
        {
            return (null, [Invalid("off", "The message says nothing about what is off.")]);
        }
        List<Mistake> mistakes = [];
        if (message.Number < 0)
        {
            mistakes.Add(Invalid("number", "The number is below 0."));
        }
        mistakes.AddRange(
            message
                .Off.Where(item => !CanBeOff.Contains(item.Kind) || string.IsNullOrWhiteSpace(item.Id))
                .Select(item => Invalid("off", $"'{item.Kind} {item.Id}' cannot be off: only an article, a procedure or an action can."))
        );
        return mistakes.Count == 0 ? (message, []) : (null, mistakes);
    }

    private static Mistake Invalid(string path, string message) =>
        new(
            MistakeCodes.AvailabilityInvalid,
            ValidationStage.Version,
            new ResourceRef(ResourceKind.Definition, string.Empty),
            path,
            message,
            "Send a whole number from 0 and everything that is off, each with its kind and id.",
            []
        );
}
