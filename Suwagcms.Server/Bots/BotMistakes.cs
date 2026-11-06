using Suwagcms.Contracts.Bots;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Server.Bots;

internal static class BotMistakes
{
    public const string IdInvalid = "bot.idInvalid";
    public const string IdTaken = "bot.idTaken";
    public const string NameMissing = "bot.nameMissing";
    public const string AddressInvalid = "bot.addressInvalid";

    public static List<Mistake> In(RegisterBot request)
    {
        var mistakes = new List<Mistake>();
        var id = request.Id ?? "";
        if (!IsValidId(id))
        {
            mistakes.Add(
                Make(
                    IdInvalid,
                    id,
                    "id",
                    "The id is missing or not valid: it starts with a lowercase letter and holds only lowercase letters, digits and hyphens.",
                    "Send an id like 'rzeczoznawca' or 'sklep-2'."
                )
            );
        }
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            mistakes.Add(Make(NameMissing, id, "name", "The name is blank.", "Send the name people should see, like 'Rzeczoznawca'."));
        }
        if (!IsAbsoluteWebAddress(request.Address))
        {
            mistakes.Add(
                Make(
                    AddressInvalid,
                    id,
                    "address",
                    "The address is not an absolute http or https address.",
                    "Send the address where the CMS reaches the bot, like 'http://rzeczoznawca.internal:8080'."
                )
            );
        }
        return mistakes;
    }

    public static Mistake Taken(string id) =>
        Make(
            IdTaken,
            id,
            "id",
            $"A bot with the id '{id}' already exists.",
            "Choose another id: the id of a registered bot does not change."
        );

    private static bool IsValidId(string id) =>
        id.Length > 0 && id[0] is >= 'a' and <= 'z' && id.All(letter => letter is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');

    private static bool IsAbsoluteWebAddress(string? address) =>
        Uri.TryCreate(address?.Trim(), UriKind.Absolute, out var parsed) && parsed.Scheme is "http" or "https" && parsed.Host.Length > 0;

    // Why: the SDK's mistake has no bot kind, and a bot is the owner of a definition, so the definition kind names it.
    private static Mistake Make(string code, string id, string path, string message, string fix) =>
        new(code, ValidationStage.Save, new ResourceRef(ResourceKind.Definition, id), path, message, fix, []);
}
