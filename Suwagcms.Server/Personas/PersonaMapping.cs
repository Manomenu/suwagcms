using System.Text.Json;
using Suwagcms.Contracts.Personas;
using Suwagcms.Sdk.Format.Personas;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Personas;

internal static class PersonaMapping
{
    public static readonly ResourceRef Target = new(ResourceKind.Persona, Persona.PublicId);

    // Why: white space is the only thing changed in a text, and only to say it is empty: the rest is kept as written, so an empty part is left out of a version.
    public static Persona ToRecord(SavePersona body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var scope = new PersonaScope
        {
            InScope = Written(body.Scope?.InScope),
            OutOfScope = Written(body.Scope?.OutOfScope),
            WhenOutOfScope = Written(body.Scope?.WhenOutOfScope),
        };
        return new Persona { Personality = Written(body.Personality), Scope = scope == new PersonaScope() ? null : scope };
    }

    // Why: the strict reader names an unknown field at the top only, and a typo inside the scope must be named just the same.
    public static (SavePersona? Body, IReadOnlyList<Mistake> Mistakes) Read(JsonElement root, ResourceRef target)
    {
        var (body, mistakes) = StrictBody.Read<SavePersona>(root, target);
        var inScope =
            root.ValueKind == JsonValueKind.Object && root.TryGetProperty("scope", out var scope) && scope.ValueKind == JsonValueKind.Object
                ? StrictBody.Read<PersonaScope>(scope, target).Mistakes.Where(mistake => mistake.Code == StrictBody.UnknownField).ToList()
                : [];
        return inScope.Count == 0
            ? (body, mistakes)
            : (
                null,
                [
                    .. mistakes.Where(mistake => mistake.Code == StrictBody.UnknownField),
                    .. inScope.Select(mistake => mistake with { Path = $"scope.{mistake.Path}" }),
                ]
            );
    }

    public static PersonaView Show(Persona? persona, int revision) => new(persona?.Personality, persona?.Scope, revision);

    private static string? Written(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
