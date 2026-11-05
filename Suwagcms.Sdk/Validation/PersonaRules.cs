using System.Globalization;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Validation;

internal static class PersonaRules
{
    // Why: the limit stops a mistake, such as a whole document pasted into a field, and stands well above any real text.
    private const int LongestText = 20_000;

    public static IReadOnlyList<Rule> All { get; } = [new(MistakeCodes.PersonaTextTooLong, ValidationStage.Save, TooLong)];

    private static IEnumerable<Mistake> TooLong(BotDefinition definition) =>
        (definition.Persona?.Texts() ?? [])
            .Where(text => text.Text?.Length > LongestText)
            .Select(text => new Mistake(
                MistakeCodes.PersonaTextTooLong,
                ValidationStage.Save,
                new ResourceRef(ResourceKind.Persona, Persona.PublicId),
                text.Path,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"'{text.Path}' has {text.Text?.Length} characters, above the limit of {LongestText}."
                ),
                string.Create(CultureInfo.InvariantCulture, $"Shorten '{text.Path}' to {LongestText} characters at most."),
                []
            ));
}
