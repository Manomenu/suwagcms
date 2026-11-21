using System.Text.RegularExpressions;
using Suwbackkit.Cli;

namespace Suwagcms.Cli.Tests.Plugin;

internal static partial class CommandList
{
    [GeneratedRegex(@"^- `suwagcms ([^`]+)`", RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ListedLine();

    public static IReadOnlyList<string> Listed(string skillText) =>
        [
            .. ListedLine()
                .Matches(SkillText.Section(skillText, "Commands"))
                .Select(match => string.Join(" ", match.Groups[1].Value.Split(' ').TakeWhile(word => word[0] is not ('<' or '[' or '-'))))
                .Order(StringComparer.Ordinal),
        ];

    public static IReadOnlyList<string> RunnableInCli() =>
        [
            .. CommandTree
                .All(CliApp.Create(CliEnvironment.Real()))
                .Where(command => command.Subcommands.Count == 0)
                .Select(CommandTree.WordsOf)
                .Order(StringComparer.Ordinal),
        ];

    public static IReadOnlyList<string> ExistingInCli() =>
        [.. CommandTree.All(CliApp.Create(CliEnvironment.Real())).Select(CommandTree.WordsOf).Order(StringComparer.Ordinal)];

    public static CommandDifferences Compare(string skillText, IEnumerable<string> runnable, IEnumerable<string> existing)
    {
        var listed = Listed(skillText);
        return new CommandDifferences([.. runnable.Except(listed)], [.. listed.Except(existing)]);
    }

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
        CommandTree
            .All(CliApp.Create(CliEnvironment.Real()))
            .Where(command => command.Subcommands.Count == 0)
            .Select(command =>
                (
                    CommandTree.WordsOf(command),
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
}
