namespace Suwagcms.Cli.Tests.Plugin;

public sealed class CommandListTests
{
    private static string MainSkill() =>
        File.ReadAllText(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", "suwagcms", "SKILL.md"));

    private const string TwoCommands =
        "## Commands\n\n- `suwagcms status` - a\n- `suwagcms server <url>` - b\n\nEvery command takes `--json`.\n";

    [Fact]
    [Trait("criterion", "AC-claude-plugin-3")]
    public void Every_command_of_the_cli_has_a_line_in_the_main_skill()
    {
        var differences = CommandList.Compare(MainSkill(), CommandList.RunnableInCli(), CommandList.ExistingInCli());

        Assert.True(
            differences.NotListed.Count == 0,
            $"The CLI has commands the main skill does not list: {string.Join(", ", differences.NotListed)}. "
                + "Add a line `- `suwagcms <command>` - what it does` under \"## Commands\" in claude-plugin/skills/suwagcms/SKILL.md."
        );
    }

    [Fact]
    [Trait("criterion", "AC-claude-plugin-3")]
    public void A_command_with_no_line_is_reported_by_name()
    {
        var differences = CommandList.Compare(TwoCommands, ["status", "server", "facts get"], ["status", "server", "facts", "facts get"]);

        Assert.Equal(["facts get"], differences.NotListed);
    }

    [Fact]
    [Trait("criterion", "AC-claude-plugin-4")]
    public void Every_line_of_the_main_skill_names_a_command_the_cli_has()
    {
        var differences = CommandList.Compare(MainSkill(), CommandList.RunnableInCli(), CommandList.ExistingInCli());

        Assert.True(
            differences.NotInCli.Count == 0,
            $"The main skill lists commands the CLI does not have: {string.Join(", ", differences.NotInCli)}. "
                + "Delete the line (or fix the name) under \"## Commands\" in claude-plugin/skills/suwagcms/SKILL.md."
        );
    }

    [Fact]
    [Trait("criterion", "AC-claude-plugin-4")]
    public void A_line_for_a_command_the_cli_lacks_is_reported_by_name()
    {
        var differences = CommandList.Compare(TwoCommands, ["status"], ["status"]);

        Assert.Equal(["server"], differences.NotInCli);
    }

    [Fact]
    [Trait("criterion", "AC-version-export-10")]
    public void The_main_skill_has_a_line_for_export() => Assert.Contains("export", CommandList.Listed(MainSkill()));

    [Fact]
    public void The_main_skill_lists_at_least_the_commands_the_cli_has_today()
    {
        Assert.Contains("status", CommandList.Listed(MainSkill()));
        Assert.Contains("server", CommandList.Listed(MainSkill()));
    }
}
