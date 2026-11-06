using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.Facts;

namespace Suwagcms.Sdk.BotRuntime.Actions;

// Why: the CMS checks values before it bothers a bot and the runner before it sends anything, so both give the same words.
public static class ActionValues
{
    public static IReadOnlyList<ActionValueUnfit> Check(BotAction action, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(values);
        var unfit = new List<ActionValueUnfit>();
        foreach (var (name, field) in action.ParameterFields)
        {
            var fact = field.Named(name);
            if (!values.TryGetValue(name, out var value))
            {
                unfit.Add(new ActionValueUnfit(name, $"Parameter '{name}' has no value.", $"Give it a value. {FactValues.Valid(fact)}"));
            }
            else if (!FactValues.Accepts(fact, value))
            {
                unfit.Add(new ActionValueUnfit(name, $"The value of parameter '{name}' does not fit.", FactValues.Valid(fact)));
            }
        }
        foreach (var name in values.Keys.Where(name => !action.ParameterFields.ContainsKey(name)).Order(StringComparer.Ordinal))
        {
            unfit.Add(
                new ActionValueUnfit(
                    name,
                    $"Action '{action.Id}' has no parameter '{name}'.",
                    action.ParameterFields.Count == 0
                        ? "Leave it out: this action takes no parameters."
                        : $"Its parameters: {string.Join(", ", action.ParameterFields.Keys.Order(StringComparer.Ordinal))}."
                )
            );
        }
        return unfit;
    }
}
