using System.CommandLine;
using Suwbackkit.Cli;

namespace Suwagcms.Cli.Tests.CommandLine;

public sealed class HelpTests
{
    private static RootCommand Tree() => CliApp.Create(CliEnvironment.Real());

    [Fact]
    [Trait("criterion", "AC-cli-foundation-11")]
    public async Task Help_on_the_root_lists_every_command()
    {
        var result = await TestCli.Run(["--help"]);

        Assert.Equal(0, result.ExitCode);
        var commands = Tree().Subcommands.Select(command => command.Name).ToList();
        Assert.NotEmpty(commands);
        foreach (var command in commands)
        {
            Assert.Contains(command, result.Output, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-11")]
    public async Task Help_on_any_command_lists_its_options_and_the_options_every_command_has()
    {
        var root = Tree();
        var everyCommand = root.Options.Where(option => option.Recursive).Select(option => option.Name);

        foreach (var command in CommandTree.All(root))
        {
            var result = await TestCli.Run([.. CommandTree.WordsOf(command).Split(' '), "--help"]);

            Assert.Equal(0, result.ExitCode);
            foreach (var option in command.Options.Select(option => option.Name).Concat(everyCommand))
            {
                Assert.Contains(option, result.Output, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-11")]
    public async Task Help_says_what_a_command_does()
    {
        var result = await TestCli.Run(["status", "--help"]);

        var description = Tree().Subcommands.Single(command => command.Name == "status").Description;
        Assert.NotNull(description);
        Assert.Contains(description, result.Output, StringComparison.Ordinal);
    }
}
