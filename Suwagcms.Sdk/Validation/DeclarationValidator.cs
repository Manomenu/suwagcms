using System.Globalization;
using System.Text.Json;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Validation;

// Why: a bot's code decides what it declares, so nothing here runs at Save: one deployment of a bot must never stop anyone from saving.
public static class DeclarationValidator
{
    public static (BotDeclaration? Declaration, IReadOnlyList<Mistake> Mistakes) ParseJson(string? json)
    {
        try
        {
            var declaration = JsonSerializer.Deserialize<BotDeclaration>(json ?? string.Empty, DefinitionFormat.Options);
            if (declaration is null)
            {
                return (null, [Whole(MistakeCodes.DeclarationInvalid, "$", "The declaration is empty.", "Send one JSON object.")]);
            }

            return HasNullItem(declaration)
                ? (
                    null,
                    [
                        Whole(
                            MistakeCodes.DeclarationInvalid,
                            "codeActions",
                            "A list of code actions, parameters or results holds null.",
                            "Remove the null or replace it with an object."
                        ),
                    ]
                )
                : (declaration, []);
        }
        catch (JsonException exception)
        {
            return (
                null,
                [
                    Whole(
                        MistakeCodes.DeclarationInvalid,
                        exception.Path ?? "$",
                        $"The declaration does not fit its contract: {exception.Message}",
                        "Answer with the shape in bot-runtime.schema.json: bot, formatVersion, blocks and codeActions."
                    ),
                ]
            );
        }
    }

    // Why: System.Text.Json accepts null list items even with nullable annotations respected.
    private static bool HasNullItem(BotDeclaration declaration) =>
        declaration.CodeActions.Any(action =>
            action is null || action.Parameters.Any(field => field is null) || action.Results.Any(field => field is null)
        );

    public static IReadOnlyList<Mistake> CheckAnswer(BotDeclaration declaration, string bot)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        List<Mistake> mistakes = [];
        if (declaration.Bot != bot)
        {
            mistakes.Add(
                Whole(
                    MistakeCodes.DeclarationBot,
                    "bot",
                    $"The address answers as bot '{declaration.Bot}', not '{bot}'.",
                    $"Register the right address for bot '{bot}', or set the bot's own id to '{bot}'."
                )
            );
        }

        return declaration.DeclaresNothing
            ? mistakes
            : [.. mistakes, .. Format(declaration), .. declaration.CodeActions.SelectMany(Action), .. Repeated(declaration)];
    }

    // Why: the blocks of a format the CMS does not read cannot be judged, so a format mismatch is the only thing said about them.
    public static IReadOnlyList<Mistake> CheckDefinition(BotDefinition definition, BotDeclaration? declaration)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (declaration is null || declaration.DeclaresNothing)
        {
            return [];
        }

        if (declaration.FormatVersion != definition.FormatVersion)
        {
            return
            [
                new(
                    MistakeCodes.DeclarationFormatVersion,
                    ValidationStage.Version,
                    new ResourceRef(ResourceKind.Definition, string.Empty),
                    "formatVersion",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"This definition is format {definition.FormatVersion} and the bot wants format {declaration.FormatVersion}."
                    ),
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Update Suwagcms.Sdk on the CMS, or declare format {definition.FormatVersion} in the bot."
                    ),
                    []
                ),
            ];
        }

        var declared = string.Join(", ", declaration.Blocks);
        return
        [
            .. DeclarationContract.Check(definition, declaration),
            .. MissingPersona(definition, declaration),
            .. definition
                .Procedures.OrderBy(procedure => procedure.Id, StringComparer.Ordinal)
                .SelectMany(procedure =>
                    procedure
                        .Steps.OrderBy(step => step.Key, StringComparer.Ordinal)
                        .SelectMany(step =>
                            step.Value.Blocks.Where(block => !declaration.Blocks.Contains(block.Name, StringComparer.Ordinal))
                                .Select(block =>
                                    ProcedureMistake.Make(
                                        procedure,
                                        MistakeCodes.DeclarationBlock,
                                        ValidationStage.Version,
                                        $"steps.{step.Key}.{block.Name}",
                                        $"Step '{step.Key}' of procedure '{procedure.Id}' uses block '{block.Name}', which this bot does not declare. It declares: {declared}.",
                                        $"Use one of the blocks the bot declares in this step, or add '{block.Name}' to the bot's setup and deploy."
                                    )
                                )
                        )
                ),
        ];
    }

    private static IEnumerable<Mistake> MissingPersona(BotDefinition definition, BotDeclaration declaration)
    {
        if (declaration.Persona == BotDeclaration.PersonaRequired && definition.Persona?.HasText() != true)
        {
            yield return new(
                MistakeCodes.PersonaRequired,
                ValidationStage.Version,
                new ResourceRef(ResourceKind.Persona, Persona.PublicId),
                "persona",
                "This bot declares 'persona: required', and this definition has no persona.",
                "Fill in the personality or the scope on the Persona page or with `suwagcms persona edit --file <file>`, or take 'persona: required' out of the bot's setup and deploy.",
                []
            );
        }
        if (declaration.Persona == BotDeclaration.PersonaComplete)
        {
            foreach (var (path, _) in (definition.Persona ?? new Persona()).Texts().Where(text => string.IsNullOrWhiteSpace(text.Text)))
            {
                yield return new(
                    MistakeCodes.PersonaIncomplete,
                    ValidationStage.Version,
                    new ResourceRef(ResourceKind.Persona, Persona.PublicId),
                    path,
                    $"This bot declares 'persona: complete', and '{path}' is empty.",
                    $"Fill in '{path}' on the Persona page or with `suwagcms persona edit --file <file>`, or change 'persona: complete' in the bot's setup and deploy.",
                    []
                );
            }
        }
    }

    private static IEnumerable<Mistake> Format(BotDeclaration declaration)
    {
        if (declaration.Persona is { } persona && persona is not (BotDeclaration.PersonaRequired or BotDeclaration.PersonaComplete))
        {
            yield return Whole(
                MistakeCodes.DeclarationPersonaUnknown,
                "persona",
                $"'{persona}' is not something a bot can say about a persona.",
                $"Say 'persona: {BotDeclaration.PersonaRequired}' or 'persona: {BotDeclaration.PersonaComplete}', or leave it out if the bot works without one."
            );
        }

        if (declaration.FormatVersion is null)
        {
            yield return Whole(
                MistakeCodes.DeclarationFormatMissing,
                "formatVersion",
                "The bot does not say which format it wants.",
                "State formatVersion in the bot's setup."
            );
        }

        if (declaration.Blocks.Count == 0)
        {
            yield return Whole(
                MistakeCodes.DeclarationBlocksMissing,
                "blocks",
                "The bot declares no blocks.",
                "State the blocks its engine can run in the bot's setup, or leave the whole declaration out."
            );
        }

        // Why: the CMS knows only the blocks of its own format, so another format's names are stored as the bot said them.
        if (declaration.FormatVersion == DefinitionFormat.Version)
        {
            foreach (var block in declaration.Blocks.Where(block => !Block.Names.Contains(block, StringComparer.Ordinal)))
            {
                yield return Whole(
                    MistakeCodes.DeclarationBlockUnknown,
                    "blocks",
                    string.Create(CultureInfo.InvariantCulture, $"Format {DefinitionFormat.Version} has no block '{block}'."),
                    $"Declare only blocks of this format: {string.Join(", ", Block.Names)}."
                );
            }
        }
    }

    private static IEnumerable<Mistake> Action(CodeAction action)
    {
        if (string.IsNullOrWhiteSpace(action.Id))
        {
            yield return OfAction(
                MistakeCodes.CodeActionIdMissing,
                action,
                "id",
                "A code action has no id.",
                "Give each code action the id a procedure will name it by."
            );
        }
        else if (!ResourceIds.IsCamelCase(action.Id))
        {
            yield return OfAction(
                MistakeCodes.CodeActionIdInvalid,
                action,
                "id",
                $"'{action.Id}' is not an action id: it is one camelCase word.",
                $"Rename the code action to '{ResourceIds.Suggest(action.Id)}'."
            );
        }

        if (string.IsNullOrWhiteSpace(action.Description))
        {
            yield return OfAction(
                MistakeCodes.CodeActionDescriptionMissing,
                action,
                "description",
                $"Code action '{action.Id}' has no description.",
                "Add one: an author reads it to choose the action."
            );
        }

        foreach (var mistake in Fields(action, "parameters", action.Parameters).Concat(Fields(action, "results", action.Results)))
        {
            yield return mistake;
        }

        foreach (var mistake in FieldNames(action, "parameters", action.Parameters).Concat(FieldNames(action, "results", action.Results)))
        {
            yield return mistake;
        }
    }

    // Why: the CMS keeps a code action's fields by name, so a name that is no name, or is said twice, cannot be kept; the answer is refused and the last good one stands.
    private static IEnumerable<Mistake> FieldNames(CodeAction action, string list, IReadOnlyList<CodeActionField> fields)
    {
        foreach (
            var name in fields.Select(field => field.Name).Where(name => !ResourceIds.IsCamelCase(name)).Distinct(StringComparer.Ordinal)
        )
        {
            yield return OfAction(
                MistakeCodes.CodeActionFieldInvalid,
                action,
                $"{list}.{name}",
                $"'{name}' in {list} of code action '{action.Id}' is not one camelCase word.",
                $"Rename it to '{ResourceIds.Suggest(name)}'."
            );
        }

        foreach (var group in fields.GroupBy(field => field.Name, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            yield return OfAction(
                MistakeCodes.CodeActionFieldRepeated,
                action,
                $"{list}.{group.Key}",
                $"'{group.Key}' is listed {string.Create(CultureInfo.InvariantCulture, $"{group.Count()}")} times in {list} of code action '{action.Id}'.",
                "Keep one of them, or give each its own name."
            );
        }
    }

    // Why: a field is typed like a fact, so the rules of facts judge it and it fails with their codes.
    private static IEnumerable<Mistake> Fields(CodeAction action, string list, IReadOnlyList<CodeActionField> fields)
    {
        var facts = new BotDefinition
        {
            FormatVersion = DefinitionFormat.Version,
            Facts =
            [
                .. fields.Select(field => new GlobalFact
                {
                    Name = field.Name,
                    Type = field.Type,
                    Description = field.Description,
                    Values = field.Values,
                    Min = field.Min,
                    Max = field.Max,
                    MaxLength = field.MaxLength,
                }),
            ],
        };
        return FactRules
            .All.SelectMany(rule => rule.Check(facts))
            .Select(mistake =>
                mistake with
                {
                    Stage = ValidationStage.Version,
                    Resource = new ResourceRef(ResourceKind.Action, action.Id),
                    Path = $"{list}.{mistake.Resource.Id}.{mistake.Path}",
                }
            );
    }

    private static IEnumerable<Mistake> Repeated(BotDeclaration declaration) =>
        declaration
            .CodeActions.GroupBy(action => action.Id, StringComparer.Ordinal)
            .Where(group => group.Key.Length > 0 && group.Count() > 1)
            .Select(group =>
                OfAction(
                    MistakeCodes.CodeActionRepeated,
                    group.First(),
                    "id",
                    string.Create(CultureInfo.InvariantCulture, $"Code action '{group.Key}' is declared {group.Count()} times."),
                    "Keep one of them, or give each its own id."
                )
            );

    private static Mistake Whole(string code, string path, string message, string fix) =>
        new(code, ValidationStage.Version, new ResourceRef(ResourceKind.Definition, string.Empty), path, message, fix, []);

    private static Mistake OfAction(string code, CodeAction action, string path, string message, string fix) =>
        new(code, ValidationStage.Version, new ResourceRef(ResourceKind.Action, action.Id), path, message, fix, []);
}
