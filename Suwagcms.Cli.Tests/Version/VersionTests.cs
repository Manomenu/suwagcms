using System.Xml.Linq;

namespace Suwagcms.Cli.Tests.Version;

public sealed class VersionTests
{
    [Fact]
    [Trait("criterion", "AC-cli-foundation-1")]
    public async Task Version_prints_a_version_and_exits_0()
    {
        var result = await TestCli.Run(["--version"]);

        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"^\d+\.\d+\.\d+", result.Output);
        Assert.Equal("", result.Error);
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-1")]
    public void The_project_packs_as_a_tool_whose_command_is_suwagcms()
    {
        var properties = XDocument
            .Load(CliProject())
            .Descendants("PropertyGroup")
            .Elements()
            .ToDictionary(element => element.Name.LocalName, element => element.Value);

        Assert.Equal("true", properties["PackAsTool"]);
        Assert.Equal("suwagcms", properties["ToolCommandName"]);
        Assert.Equal("suwagcms", properties["AssemblyName"]);
    }

    private static string CliProject()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var project = Path.Combine(directory.FullName, "Suwagcms.Cli", "Suwagcms.Cli.csproj");
            if (File.Exists(project))
            {
                return project;
            }
        }
        throw new InvalidOperationException("Suwagcms.Cli.csproj was not found above the test output");
    }
}
