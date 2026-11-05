using System.Globalization;
using System.Text.Json;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.BotRuntime;

internal static class VersionIntake
{
    public static IReadOnlyList<Mistake> Check(string json, string botId, int number, BotDeclaration? declaration = null)
    {
        // Why: the bot's own declaration judges what it loads, so a version that needs what the bot no longer has is refused here too, whatever the CMS believed when it made it.
        var content = DefinitionValidator.ValidateJson(json, ValidationStage.Version, declaration);
        // Why: without a readable formatVersion the document is not an object of this format, so its envelope says nothing.
        if (content.Any(mistake => mistake.Code is MistakeCodes.FormatInvalid or MistakeCodes.FormatVersion))
        {
            return content;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        List<Mistake> mistakes = [];

        if (number < 1)
        {
            mistakes.Add(Envelope(MistakeCodes.VersionNumber, "number", "The route's number is below 1.", "Use a whole number from 1."));
        }

        if (Text(root, "bot") != botId)
        {
            mistakes.Add(
                Envelope(
                    MistakeCodes.VersionBot,
                    "bot",
                    $"The document is for bot '{Text(root, "bot")}', this bot is '{botId}'.",
                    "Push the version to the bot it was created for."
                )
            );
        }

        if (!root.TryGetProperty("number", out var written) || !written.TryGetInt32(out var inDocument) || inDocument != number)
        {
            mistakes.Add(
                Envelope(
                    MistakeCodes.VersionNumber,
                    "number",
                    $"The document's number differs from the route's {number.ToString(CultureInfo.InvariantCulture)}.",
                    "Send the document to the route of its own number."
                )
            );
        }

        var expected = VersionDigest.Of(root);
        if (Text(root, "digest") != expected)
        {
            mistakes.Add(
                Envelope(
                    MistakeCodes.VersionDigest,
                    "digest",
                    $"The digest does not match the document (expected {expected}).",
                    "Compute it with VersionDigest.Of, or create the version again."
                )
            );
        }

        return [.. mistakes, .. content];
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static Mistake Envelope(string code, string path, string message, string fix) =>
        new(code, ValidationStage.Version, new ResourceRef(ResourceKind.Definition, string.Empty), path, message, fix, []);
}
