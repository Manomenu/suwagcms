using System.Globalization;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.References;

namespace Suwagcms.Sdk.Validation;

internal static class DeclarationContract
{
    public static IEnumerable<Mistake> Check(BotDefinition definition, BotDeclaration declaration)
    {
        var declared = declaration
            .CodeActions.DistinctBy(action => action.Id, StringComparer.Ordinal)
            .ToDictionary(action => action.Id, action => action.ToAction(), StringComparer.Ordinal);
        foreach (var action in definition.Actions.OrderBy(action => action.Id, StringComparer.Ordinal))
        {
            declared.TryGetValue(action.Id, out var said);
            if (action.IsCode && said is null)
            {
                yield return Undeclared(definition, action);
            }
            else if (action.IsCode && said is not null)
            {
                var differences = Differences("parameter", action.ParameterFields, said.ParameterFields)
                    .Concat(Differences("result", action.ResultFields, said.ResultFields))
                    .ToList();
                if (differences.Count > 0)
                {
                    yield return Make(
                        MistakeCode.ActionCodeContract,
                        action,
                        $"Code action '{action.Id}' differs from what the bot declares: {string.Join("; ", differences)}.",
                        "Ask the bot again so the CMS brings its copy in line, or change the bot and deploy."
                    );
                }
            }
            else if (said is not null)
            {
                yield return Make(
                    MistakeCode.ActionCodeConflict,
                    action,
                    $"Action '{action.Id}' is an HTTP action here, and the bot also declares a code action of that id: one id has two sources.",
                    $"Remove or rename the HTTP action '{action.Id}', or rename the code action in the bot."
                );
            }
        }
    }

    private static Mistake Undeclared(BotDefinition definition, BotAction action)
    {
        var target = new ResourceRef(ResourceKind.Action, action.Id);
        var steps = ReferenceWalker.From(definition).Where(reference => reference.To == target).ToList();
        var where = string.Join(", ", steps.Select(reference => $"{reference.Path} of procedure '{reference.From.Id}'"));
        return Make(
            MistakeCode.ActionCodeUndeclared,
            action,
            steps.Count == 0
                ? $"The bot no longer has code action '{action.Id}'."
                : $"The bot no longer has code action '{action.Id}', which is used by: {where}.",
            "Change these steps so they no longer use it, or declare it again in the bot and deploy.",
            [.. steps.Select(reference => new RelatedResource(reference.From, reference.Path))]
        );
    }

    private static IEnumerable<string> Differences(
        string noun,
        IReadOnlyDictionary<string, ActionField> copy,
        IReadOnlyDictionary<string, ActionField> said
    )
    {
        foreach (var name in copy.Keys.Union(said.Keys).Order(StringComparer.Ordinal))
        {
            copy.TryGetValue(name, out var mine);
            said.TryGetValue(name, out var theirs);
            if (theirs is null)
            {
                yield return $"{noun} '{name}' is not declared";
            }
            else if (mine is null)
            {
                yield return $"{noun} '{name}' is declared and not here";
            }
            else if (Contract(mine) != Contract(theirs))
            {
                yield return $"{noun} '{name}' is {Contract(mine)} here and {Contract(theirs)} in the bot";
            }
        }
    }

    public static string Contract(ActionField field) =>
        field.Type switch
        {
            FactTypes.Enum => $"enum of {string.Join(", ", field.Values ?? [])}",
            FactTypes.WholeNumber => string.Create(CultureInfo.InvariantCulture, $"int from {field.Min} to {field.Max}"),
            FactTypes.Text => string.Create(CultureInfo.InvariantCulture, $"text up to {field.MaxLength}"),
            _ => field.Type,
        };

    private static Mistake Make(
        MistakeCode code,
        BotAction action,
        string message,
        string fix,
        IReadOnlyList<RelatedResource>? related = null
    ) => new(code, ValidationStage.Version, new ResourceRef(ResourceKind.Action, action.Id), "implementation", message, fix, related ?? []);
}
