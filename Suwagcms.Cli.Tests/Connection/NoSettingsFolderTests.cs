namespace Suwagcms.Cli.Tests.Connection;

public sealed class NoSettingsFolderTests
{
    [Fact]
    public async Task Saving_without_a_settings_folder_is_refused_with_the_fix_and_writes_nothing_in_the_current_directory()
    {
        var before = Directory.GetFileSystemEntries(Directory.GetCurrentDirectory());

        var result = await TestCli.Run(["server", "http://cms.internal"], settingsFolder: "");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("HOME", result.Error, StringComparison.Ordinal);
        Assert.Contains("--server", result.Error, StringComparison.Ordinal);
        Assert.Equal(before, Directory.GetFileSystemEntries(Directory.GetCurrentDirectory()));
    }

    [Fact]
    public async Task Reading_works_without_a_settings_folder()
    {
        var server = StubServer.Healthy();

        var result = await TestCli.Run(["status"], server, settingsFolder: "");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://localhost:3330/api/health", Assert.Single(server.Requests).ToString());
    }
}
