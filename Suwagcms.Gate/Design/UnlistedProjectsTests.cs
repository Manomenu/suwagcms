using System.Text.RegularExpressions;

namespace Suwagcms.Gate.Design;

// Why: a guard over a list passes in silence for whatever is left off the list, so what the list leaves out has to fail by itself
public sealed partial class UnlistedProjectsTests
{
    // Why: the gate holds the guards; the SDK, the contracts and the CLI are flat, one folder per resource kind, with no order inside to keep
    private static readonly string[] WithoutLayers = ["Suwagcms.Gate", "Suwagcms.Sdk", "Suwagcms.Contracts", "Suwagcms.Cli"];

    [GeneratedRegex("<AssemblyName>([^<]+)</AssemblyName>")]
    private static partial Regex AssemblyNameElement();

    private static IEnumerable<string> ProjectsWithProductionCode() =>
        LayersFile.Projects().Keys.Where(project => !project.EndsWith(".Tests", StringComparison.Ordinal));

    private static string AssemblyOf(string project) =>
        AssemblyNameElement().Match(RepositoryFiles.Read($"{project}/{project}.csproj")) is { Success: true } named
            ? named.Groups[1].Value
            : project;

    [Fact]
    public void Every_project_is_read_by_the_guards_over_types()
    {
        var read = SolutionTypes.Assemblies.Select(assembly => assembly.GetName().Name).ToHashSet();
        var unread = ProjectsWithProductionCode().Where(project => !read.Contains(AssemblyOf(project))).Order().ToList();

        Assert.True(
            unread.Count == 0,
            $"add typeof(<a public type of the project>).Assembly to Assemblies in Suwagcms.Gate/Design/SolutionTypes.cs, or the naming, interface and obsolete guards never see it: {string.Join(", ", unread)}"
        );
    }

    [Fact]
    public void Every_project_has_layers()
    {
        var unlayered = ProjectsWithProductionCode().Except(WithoutLayers).Except(ContractsTests.LayeredProjects).Order().ToList();

        Assert.True(
            unlayered.Count == 0,
            $"give the project its layers in Suwagcms.Gate/layers.json and tests like serverLayers has in ContractsTests, then add it to LayeredProjects in ContractsTests: {string.Join(", ", unlayered)}"
        );
    }
}
