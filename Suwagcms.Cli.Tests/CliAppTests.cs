using System.CommandLine;

namespace Suwagcms.Cli.Tests;

public sealed class CliAppTests
{
    [Fact]
    public async Task VersionPrintsAVersionAndSucceeds()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await CliApp.Run(["--version"], new InvocationConfiguration { Output = output, Error = error });

        Assert.Equal(0, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(output.ToString()));
        Assert.Equal("", error.ToString());
    }
}
