using Suwagcms.Contracts;

namespace Suwagcms.Cli.Actions;

internal static class ActionText
{
    public static string Table(IReadOnlyList<ActionView> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        if (actions.Count == 0)
        {
            return "No actions yet. Write one in a YAML file and add it: suwagcms actions add --file <file>";
        }
        var idWidth = actions.Max(action => action.Id.Length);
        var descriptionWidth = actions.Max(action => action.Description.Length);
        return string.Join(
            Environment.NewLine,
            actions.Select(action =>
                $"{action.Id.PadRight(idWidth)}  {action.Description.PadRight(descriptionWidth)}  {Kind(action)}".TrimEnd()
            )
        );
    }

    public static string Kind(ActionView action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action.Implementation is { IsCode: true }
            ? action.NoLongerDeclared
                ? "code  no longer declared"
                : "code"
            : action.AlsoDeclared
                ? "http  the bot also declares this id"
                : "http";
    }
}
