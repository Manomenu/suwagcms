namespace Suwagcms.Cli.Tests.Plugin;

public sealed class MainSkillContentTests
{
    // Why: what the skill must say, as the section that says it and the words that section must hold.
    private static readonly (string Topic, string Section, string[] Words)[] Required =
    [
        ("how to install the CLI", "Install", ["dotnet tool install -g Suwagcms.Cli"]),
        ("how to pick the server", "Server and bot", ["--server", "SUWAGCMS_URL", "suwagcms server <url>"]),
        ("how to pick the bot", "Server and bot", ["--bot", "SUWAGCMS_BOT", "suwagcms use <id>"]),
        ("to use --json", "Output", ["--json", "one JSON document"]),
        ("how to read a refusal", "Refusals", ["Exit code `1`", "mistakes", "fix"]),
        (
            "how to link an article and find its target",
            "Link to an article",
            ["cms://article/<id>", "articles list --text", "reference.missing", "reference.malformed"]
        ),
        ("that resources are never edited as local files", "Never edit local files", ["never edited as a local file"]),
    ];

    private static string MainSkill() =>
        File.ReadAllText(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", "suwagcms", "SKILL.md"));

    private static List<string> Missing(string skillText) =>
        [
            .. Required
                .Where(item => item.Words.Any(word => !SkillText.Section(skillText, item.Section).Contains(word, StringComparison.Ordinal)))
                .Select(item => $"{item.Topic} (a section \"## {item.Section}\" with: {string.Join(", ", item.Words)})"),
        ];

    [Fact]
    [Trait("criterion", "AC-claude-plugin-5")]
    public void The_main_skill_says_how_to_install_pick_the_server_and_the_bot_use_json_read_a_refusal_and_never_edit_local_files()
    {
        var missing = Missing(MainSkill());

        Assert.True(
            missing.Count == 0,
            $"The main skill, claude-plugin/skills/suwagcms/SKILL.md, does not say: {string.Join("; ", missing)}."
        );
    }

    [Fact]
    [Trait("criterion", "AC-article-links-12")]
    public void The_main_skill_gives_the_link_form_and_how_to_find_a_target() =>
        Assert.DoesNotContain(Missing(MainSkill()), item => item.StartsWith("how to link an article", StringComparison.Ordinal));

    [Fact]
    [Trait("criterion", "AC-claude-plugin-5")]
    public void A_skill_that_leaves_something_out_is_reported_with_what_it_leaves_out()
    {
        var missing = Missing("## Install\n\nRun `dotnet tool install -g Suwagcms.Cli`.\n\n## Output\n\nUse `--json`.\n");

        Assert.Equal(Required.Length - 1, missing.Count);
        Assert.DoesNotContain(missing, item => item.StartsWith("how to install", StringComparison.Ordinal));
        Assert.Contains(missing, item => item.StartsWith("to use --json", StringComparison.Ordinal));
        Assert.Contains(missing, item => item.StartsWith("how to read a refusal", StringComparison.Ordinal));
    }
}
