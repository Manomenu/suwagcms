namespace Suwagcms.Cli.Tests.Connection;

public sealed class RepositoryConfigTests : IDisposable
{
    private const string Criterion = "AC-cli-foundation-16";

    private readonly DirectoryInfo repository = Directory.CreateTempSubdirectory("suwagcms-config-");

    public void Dispose() => repository.Delete(recursive: true);

    private void WriteConfig(string yaml, string folder = "suwagcms")
    {
        Directory.CreateDirectory(Path.Combine(repository.FullName, folder));
        File.WriteAllText(Path.Combine(repository.FullName, folder, "config.yaml"), yaml);
    }

    private const string Both = "suwagcmsUrl: http://from-config\nbotId: from-config-bot\n";

    private static readonly Dictionary<string, string> Variables = new() { ["SUWAGCMS_URL"] = "http://from-variable" };

    [Fact]
    [Trait("criterion", Criterion)]
    public async Task In_a_repository_with_the_file_a_plain_command_takes_its_address_from_it_with_nothing_saved()
    {
        WriteConfig(Both);
        var server = StubServer.Healthy();

        var result = await TestCli.Run(["status"], server, workingDirectory: repository.FullName);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://from-config/api/health", Assert.Single(server.Requests).ToString());
    }

    [Fact]
    [Trait("criterion", Criterion)]
    [Trait("criterion", "AC-cli-foundation-2")]
    public async Task The_option_and_the_variable_win_over_the_file_and_the_file_over_the_saved_address()
    {
        WriteConfig(Both);
        var folder = TestCli.FreshSettingsFolder();
        await TestCli.Run(["server", "http://saved"], settingsFolder: folder);
        var byOption = StubServer.Healthy();
        var byVariable = StubServer.Healthy();
        var byFile = StubServer.Healthy();

        await TestCli.Run(["status", "--server", "http://from-option"], byOption, Variables, folder, workingDirectory: repository.FullName);
        await TestCli.Run(["status"], byVariable, Variables, folder, workingDirectory: repository.FullName);
        await TestCli.Run(["status"], byFile, settingsFolder: folder, workingDirectory: repository.FullName);

        Assert.Equal("http://from-option/api/health", Assert.Single(byOption.Requests).ToString());
        Assert.Equal("http://from-variable/api/health", Assert.Single(byVariable.Requests).ToString());
        Assert.Equal("http://from-config/api/health", Assert.Single(byFile.Requests).ToString());
    }

    [Fact]
    [Trait("criterion", Criterion)]
    [Trait("criterion", "AC-bot-profiles-9")]
    public async Task The_bot_comes_from_the_file_below_the_option_and_the_variable_and_above_what_use_stored()
    {
        WriteConfig(Both);
        var folder = TestCli.FreshSettingsFolder();
        await TestCli.Run(["use", "from-stored", "--server", "http://any"], StubServer.Healthy(), settingsFolder: folder);

        var byFile = await BotAsked([], [], folder);
        var byVariable = await BotAsked([], new Dictionary<string, string> { ["SUWAGCMS_BOT"] = "from-variable" }, folder);
        var byOption = await BotAsked(
            ["--bot", "from-option"],
            new Dictionary<string, string> { ["SUWAGCMS_BOT"] = "from-variable" },
            folder
        );

        Assert.Contains("from-config-bot", byFile, StringComparison.Ordinal);
        Assert.Contains("from-variable", byVariable, StringComparison.Ordinal);
        Assert.Contains("from-option", byOption, StringComparison.Ordinal);
    }

    private async Task<string> BotAsked(string[] extra, Dictionary<string, string> variables, string folder)
    {
        var server = StubServer.Healthy();
        await TestCli.Run(["facts", "list", .. extra], server, variables, folder, workingDirectory: repository.FullName);
        return string.Join(" ", server.Requests);
    }

    [Fact]
    [Trait("criterion", Criterion)]
    public async Task The_dir_option_names_another_folder_and_one_with_no_file_is_a_wrong_use()
    {
        WriteConfig("suwagcmsUrl: http://from-other\n", "other");
        var server = StubServer.Healthy();

        var used = await TestCli.Run(["status", "--dir", "other"], server, workingDirectory: repository.FullName);
        var missing = await TestCli.Run(["status", "--dir", "nowhere"], server, workingDirectory: repository.FullName);

        Assert.Equal(0, used.ExitCode);
        Assert.Equal("http://from-other/api/health", Assert.Single(server.Requests).ToString());
        Assert.Equal(2, missing.ExitCode);
        Assert.Contains("nowhere", missing.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("server: http://wrong-key\n", "unknown key server")]
    [InlineData("- a list\n", "no mapping")]
    [InlineData("suwagcmsUrl: [unclosed\n", "cannot be read")]
    [Trait("criterion", Criterion)]
    public async Task A_file_that_is_wrong_exits_2_saying_the_fix_and_sends_nothing(string yaml, string says)
    {
        WriteConfig(yaml);
        var server = StubServer.Healthy();

        var result = await TestCli.Run(["status"], server, workingDirectory: repository.FullName);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(says, result.Error, StringComparison.Ordinal);
        Assert.Empty(server.Requests);
    }

    [Fact]
    [Trait("criterion", Criterion)]
    public async Task With_no_file_nothing_changes()
    {
        var server = StubServer.Healthy();

        var result = await TestCli.Run(["status", "--server", "http://given"], server, workingDirectory: repository.FullName);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://given/api/health", Assert.Single(server.Requests).ToString());
    }
}
