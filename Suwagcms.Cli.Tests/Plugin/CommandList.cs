using System.CommandLine;
using System.Text.RegularExpressions;

namespace Suwagcms.Cli.Tests.Plugin;

internal static partial class CommandList
{
    [GeneratedRegex(@"^- `suwagcms ([^`]+)`", RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ListedLine();

    // Why: a line names a command by its words up to the first argument or option, so `suwagcms server <url>` names `server`.
    public static IReadOnlyList<string> Listed(string skillText) =>
        [
            .. ListedLine()
                .Matches(SkillText.Section(skillText, "Commands"))
                .Select(match => string.Join(" ", match.Groups[1].Value.Split(' ').TakeWhile(word => word[0] is not ('<' or '[' or '-'))))
                .Order(StringComparer.Ordinal),
        ];

    // Why: a command that only groups others (`facts` of `facts get`) needs no line of its own; the ones it groups do.
    public static IReadOnlyList<string> RunnableInCli() =>
        [
            .. All(CliApp.Create(CliEnvironment.Real()))
                .Where(command => command.Subcommands.Count == 0)
                .Select(Words)
                .Order(StringComparer.Ordinal),
        ];

    public static IReadOnlyList<string> ExistingInCli() =>
        [.. All(CliApp.Create(CliEnvironment.Real())).Select(Words).Order(StringComparer.Ordinal)];

    public static CommandDifferences Compare(string skillText, IEnumerable<string> runnable, IEnumerable<string> existing)
    {
        var listed = Listed(skillText);
        return new CommandDifferences([.. runnable.Except(listed)], [.. listed.Except(existing)]);
    }

    private static IEnumerable<Command> All(Command parent) => parent.Subcommands.SelectMany(child => All(child).Prepend(child));

    private static string Words(Command command)
    {
        var words = new List<string>();
        for (Symbol? symbol = command; symbol is Command and not RootCommand; symbol = symbol.Parents.FirstOrDefault())
        {
            words.Insert(0, symbol.Name);
        }
        return string.Join(" ", words);
    }
}
