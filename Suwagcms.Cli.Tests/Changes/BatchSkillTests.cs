using Suwagcms.Cli.Tests.Plugin;

namespace Suwagcms.Cli.Tests.Changes;

public sealed class BatchSkillTests
{
    private static string Skill(string name) =>
        File.ReadAllText(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", name, "SKILL.md"));

    [Fact]
    [Trait("criterion", "AC-batch-apply-14")]
    public void Apply_has_its_line_in_the_main_skill() => Assert.Contains("apply", CommandList.Listed(Skill("suwagcms")));

    [Fact]
    [Trait("criterion", "AC-batch-apply-14")]
    [Trait("criterion", "AC-claude-plugin-6")]
    public void The_batch_skill_starts_with_a_dry_run()
    {
        var steps = SkillText.Section(Skill("suwagcms-batch"), "Steps").ReplaceLineEndings("\n");
        var firstApply = steps.IndexOf("suwagcms apply", StringComparison.Ordinal);

        Assert.True(firstApply >= 0, "The batch skill has no `suwagcms apply` in its section \"## Steps\".");
        var line = steps[firstApply..].Split('\n')[0];
        Assert.Contains("--dry-run", line, StringComparison.Ordinal);
        Assert.Equal("suwagcms-batch", SkillText.FrontMatter(Skill("suwagcms-batch"))["name"]);
    }
}
