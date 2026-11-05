using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Cli.Versions;
using Suwagcms.Contracts;

namespace Suwagcms.Cli.Personas;

internal static class PersonaText
{
    // Why: the form `edit --file` takes back, built from what the server sent; the revision comes last because a text ending in several line breaks must end in its block, whatever the output adds after the last line.
    public static string Shown(PersonaView persona, string bot)
    {
        ArgumentNullException.ThrowIfNull(persona);
        return IsEmpty(persona)
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"Bot {bot} has an empty persona (revision {persona.Revision}), so a version holds none. Write one with: suwagcms persona edit --file <file>"
            )
            : Yaml(persona);
    }

    public static string Saved(PersonaView persona, string bot, bool dry)
    {
        ArgumentNullException.ThrowIfNull(persona);
        var what = IsEmpty(persona) ? "an empty persona" : "the persona";
        return dry
            ? $"Would save {what} of bot {bot}. Nothing was changed."
            : string.Create(CultureInfo.InvariantCulture, $"Saved {what} of bot {bot}, revision {persona.Revision}.");
    }

    public static bool IsEmpty(PersonaView persona)
    {
        ArgumentNullException.ThrowIfNull(persona);
        return persona.Personality is null && persona.Scope is null;
    }

    private static string Yaml(PersonaView persona)
    {
        var document = new JsonObject();
        if (persona.Personality is { } personality)
        {
            document["personality"] = personality;
        }
        if (persona.Scope is { } scope)
        {
            var parts = new JsonObject();
            Add(parts, "inScope", scope.InScope);
            Add(parts, "outOfScope", scope.OutOfScope);
            Add(parts, "whenOutOfScope", scope.WhenOutOfScope);
            document["scope"] = parts;
        }
        document["revision"] = persona.Revision;
        var text = ExportYaml.Yaml(JsonSerializer.SerializeToElement(document));
        return text.EndsWith('\n') ? text[..^1] : text;
    }

    private static void Add(JsonObject parts, string name, string? text)
    {
        if (text is not null)
        {
            parts[name] = text;
        }
    }
}
