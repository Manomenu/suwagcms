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

    // Why: the lines say what an agent can pass, so an option is looked for on its command's line, or where a line or a paragraph sends the reader.
    public static IReadOnlyList<string> Unnamed(string skillText, IEnumerable<(string Words, IReadOnlyList<string> Options)> commands)
    {
        var section = SkillText.Section(skillText, "Commands").Split('\n');
        return
        [
            .. commands.SelectMany(command =>
            {
                var lines = LinesOf(section, command.Words);
                if (lines.Any(line => line.Contains("[options of add]", StringComparison.Ordinal)))
                {
                    lines = [.. lines, .. LinesOf(section, $"{command.Words.Split(' ')[0]} add")];
                }
                var dryRunParagraph = section.FirstOrDefault(line => line.Contains(" take `--dry-run`", StringComparison.Ordinal)) ?? "";
                if (dryRunParagraph.Contains($"`{command.Words}`", StringComparison.Ordinal))
                {
                    lines = [.. lines, dryRunParagraph];
                }
                return command
                    .Options.Where(option => !lines.Any(line => line.Contains(option, StringComparison.Ordinal)))
                    .Select(option => $"{command.Words} {option}");
            }),
        ];
    }

    public static IEnumerable<(string Words, IReadOnlyList<string> Options)> OptionsInCli() =>
        All(CliApp.Create(CliEnvironment.Real()))
            .Where(command => command.Subcommands.Count == 0)
            .Select(command =>
                (
                    Words(command),
                    (IReadOnlyList<string>)[.. command.Options.Where(option => !option.Recursive).Select(option => option.Name)]
                )
            );

    private static string[] LinesOf(string[] section, string words) =>
        [
            .. section.Where(line =>
                line.StartsWith($"- `suwagcms {words} ", StringComparison.Ordinal)
                || line.StartsWith($"- `suwagcms {words}`", StringComparison.Ordinal)
            ),
        ];

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
