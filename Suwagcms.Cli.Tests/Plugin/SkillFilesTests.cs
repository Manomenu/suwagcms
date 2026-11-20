namespace Suwagcms.Cli.Tests.Plugin;

public sealed class SkillFilesTests
{
    private static string[] Skills() =>
        Directory.GetFiles(Path.Combine(RepositoryRoot.Path, "claude-plugin", "skills"), "SKILL.md", SearchOption.AllDirectories);

    private static readonly string[] Keys = ["name", "description"];

    private static List<string> Lacks(string text)
    {
        var matter = SkillText.FrontMatter(text);
        return [.. Keys.Where(key => !matter.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))];
    }

    [Fact]
    [Trait("criterion", "AC-claude-plugin-2")]
    public void Every_skill_of_the_plugin_has_a_name_and_a_description()
    {
        var skills = Skills();

        Assert.NotEmpty(skills);
        foreach (var skill in skills)
        {
            var lacking = Lacks(File.ReadAllText(skill));
            Assert.True(
                lacking.Count == 0,
                $"{Path.GetRelativePath(RepositoryRoot.Path, skill)} has no {string.Join(" and ", lacking)} in its front matter: add `name:` and `description:` between the `---` lines at the top."
            );
        }
    }

    [Fact]
    [Trait("criterion", "AC-claude-plugin-2")]
    public void A_skill_without_a_name_or_a_description_is_reported()
    {
        Assert.Equal(["name", "description"], Lacks("# Just a heading"));
        Assert.Equal(["description"], Lacks("---\nname: x\ndescription:\n---\n"));
        Assert.Equal(["name"], Lacks("---\ndescription: does x\n---\n"));
        Assert.Empty(Lacks("---\nname: x\ndescription: does x\n---\n"));
    }
}
