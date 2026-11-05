using Suwagcms.Cli.Tests.Plugin;

namespace Suwagcms.Cli.Tests.Availability;

public sealed class AvailabilitySkillTests
{
    [Fact]
    [Trait("criterion", "AC-availability-21")]
    public void The_line_of_availability_off_says_it_takes_effect_at_once_and_that_a_resource_in_use_needs_yes()
    {
        var skill = File.ReadAllText(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills", "suwagcms", "SKILL.md"));
        var commands = SkillText.Section(skill, "Commands");

        var off = commands.Split('\n').Single(line => line.StartsWith("- `suwagcms availability off", StringComparison.Ordinal));
        Assert.Contains("takes effect at once", off, StringComparison.Ordinal);
        Assert.Contains("a resource in use needs `--yes`", off, StringComparison.Ordinal);
    }
}
