using System.Globalization;
using System.Text.Json;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation.Rules;

namespace Suwagcms.Sdk.Validation;

public static class DefinitionValidator
{
    private static readonly string[] ListNames = ["facts", "knowledgeBases", "articles", "actions", "procedures"];

    internal static IReadOnlyList<Rule> Rules { get; } =
    [
        DuplicateIdRule.Rule,
        .. FactRules.All,
        .. FactNameRules.All,
        .. ActionRules.All,
        .. KnowledgeBaseRules.All,
        .. ArticleRules.All,
        .. ReferenceRules.All,
        .. ProcedureShapeRules.All,
        .. ProcedureFactRules.All,
        .. ActionBlockRules.All,
        .. ProcedureVersionRules.All,
        .. KnowledgeRules.All,
        .. PersonaRules.All,
    ];

    public static IReadOnlyList<Mistake> Validate(BotDefinition definition, ValidationStage stage)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return Validate(definition, stage, Rules);
    }

    public static IReadOnlyList<Mistake> ValidateJson(string json, ValidationStage stage) => ValidateJson(json, stage, Rules);

    // Why: what a bot cannot run is checked only at Version: the working state may be unfinished, and the bot may change without anyone editing it.
    public static IReadOnlyList<Mistake> Validate(BotDefinition definition, ValidationStage stage, BotDeclaration? declaration)
    {
        var mistakes = Validate(definition, stage);
        return stage == ValidationStage.Version
            ? [.. mistakes, .. DeclarationValidator.CheckDefinition(definition, declaration)]
            : mistakes;
    }

    public static IReadOnlyList<Mistake> ValidateJson(string json, ValidationStage stage, BotDeclaration? declaration)
    {
        var mistakes = ValidateJson(json, stage);
        // Why: a document that cannot be read as this format has no steps or format version to hold against the bot.
        if (
            stage != ValidationStage.Version
            || mistakes.Any(mistake => mistake.Code is MistakeCodes.FormatInvalid or MistakeCodes.FormatVersion)
        )
        {
            return mistakes;
        }

        return [.. mistakes, .. DeclarationValidator.CheckDefinition(BotDefinition.Read(json), declaration)];
    }

    internal static IReadOnlyList<Mistake> ValidateJson(string? json, ValidationStage stage, IReadOnlyList<Rule> rules)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json ?? string.Empty);
        }
        catch (JsonException exception)
        {
            return
            [
                Envelope(
                    MistakeCodes.FormatInvalid,
                    "$",
                    $"The document is not valid JSON: {exception.Message}",
                    "Send one valid JSON object."
                ),
            ];
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return
                [
                    Envelope(
                        MistakeCodes.FormatInvalid,
                        "$",
                        "The document is not a JSON object.",
                        "Send one JSON object with formatVersion and the lists of resources."
                    ),
                ];
            }

            if (!root.TryGetProperty("formatVersion", out var version))
            {
                return [UnknownVersion("is missing")];
            }

            if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number))
            {
                return [UnknownVersion($"{version.GetRawText()} is not a whole number")];
            }

            if (number != DefinitionFormat.Version)
            {
                return [UnknownVersion(number.ToString(CultureInfo.InvariantCulture))];
            }

            if (NullItemPath(root) is { } nullItem)
            {
                return
                [
                    Envelope(
                        MistakeCodes.FormatInvalid,
                        nullItem,
                        "A list holds null instead of a resource.",
                        "Remove the null or replace it with an object."
                    ),
                ];
            }

            try
            {
                var definition = root.Deserialize<BotDefinition>(DefinitionFormat.Options);
                return definition is null
                    ? [Envelope(MistakeCodes.FormatInvalid, "$", "The document is empty.", "Send one JSON object.")]
                    : Validate(definition, stage, rules);
            }
            catch (JsonException exception)
            {
                return
                [
                    Envelope(
                        MistakeCodes.FormatInvalid,
                        exception.Path ?? "$",
                        $"The document does not fit the format: {exception.Message}",
                        "Fix the value at the path: lists hold objects, ids are strings."
                    ),
                ];
            }
        }
    }

    internal static IReadOnlyList<Mistake> Validate(BotDefinition definition, ValidationStage stage, IReadOnlyList<Rule> rules)
    {
        if (definition.FormatVersion != DefinitionFormat.Version)
        {
            return [UnknownVersion(definition.FormatVersion.ToString(CultureInfo.InvariantCulture))];
        }

        return
        [
            .. rules
                .Where(rule => rule.Stage == ValidationStage.Save || stage == ValidationStage.Version)
                .SelectMany(rule =>
                    rule.Check(definition)
                        .Select(mistake => mistake with { Stage = rule.Stage })
                        .OrderBy(mistake => mistake.Resource.Kind)
                        .ThenBy(mistake => mistake.Resource.Id, StringComparer.Ordinal)
                        .ThenBy(mistake => mistake.Path, StringComparer.Ordinal)
                ),
        ];
    }

    // Why: System.Text.Json accepts null list items even with nullable annotations respected.
    private static string? NullItemPath(JsonElement root)
    {
        foreach (var list in ListNames)
        {
            if (root.TryGetProperty(list, out var items) && items.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in items.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Null)
                    {
                        return $"$.{list}[{index}]";
                    }

                    index++;
                }
            }
        }

        return null;
    }

    private static Mistake UnknownVersion(string found) =>
        Envelope(
            MistakeCodes.FormatVersion,
            "formatVersion",
            $"formatVersion {found}; this SDK reads {DefinitionFormat.Version}.",
            $"Set formatVersion to {DefinitionFormat.Version}, or update Suwagcms.Sdk if the document is from a newer format."
        );

    private static Mistake Envelope(string code, string path, string message, string fix) =>
        new(code, ValidationStage.Save, new ResourceRef(ResourceKind.Definition, string.Empty), path, message, fix, []);
}
