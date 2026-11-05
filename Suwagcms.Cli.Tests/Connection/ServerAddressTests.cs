namespace Suwagcms.Cli.Tests.Connection;

public sealed class ServerAddressTests
{
    private static readonly Dictionary<string, string> FromVariable = new() { ["SUWAGCMS_URL"] = "http://from-variable" };

    private static async Task<Uri> AddressUsed(string[] args, Dictionary<string, string>? variables = null, string? settingsFolder = null)
    {
        var server = StubServer.Healthy();

        var result = await TestCli.Run(args, server, variables, settingsFolder);

        Assert.Equal(0, result.ExitCode);
        return Assert.Single(server.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-2")]
    public async Task The_server_option_wins_over_the_variable_the_saved_address_and_the_default()
    {
        var folder = TestCli.FreshSettingsFolder();
        await TestCli.Run(["server", "http://saved"], settingsFolder: folder);

        var request = await AddressUsed(["status", "--server", "http://from-option"], FromVariable, folder);

        Assert.Equal("http://from-option/api/health", request.ToString());
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-2")]
    public async Task The_variable_wins_over_the_saved_address_and_the_default()
    {
        var folder = TestCli.FreshSettingsFolder();
        await TestCli.Run(["server", "http://saved"], settingsFolder: folder);

        var request = await AddressUsed(["status"], FromVariable, folder);

        Assert.Equal("http://from-variable/api/health", request.ToString());
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-2")]
    public async Task The_saved_address_wins_over_the_default()
    {
        var folder = TestCli.FreshSettingsFolder();
        var saved = await TestCli.Run(["server", "http://saved:6330/"], settingsFolder: folder);

        var request = await AddressUsed(["status"], settingsFolder: folder);

        Assert.Equal(0, saved.ExitCode);
        Assert.Equal("http://saved:6330/api/health", request.ToString());
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-2")]
    public async Task With_nothing_set_the_local_default_is_used()
    {
        var request = await AddressUsed(["status"]);

        Assert.Equal("http://localhost:3330/api/health", request.ToString());
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-13")]
    public async Task An_address_that_is_not_http_is_a_wrong_use_that_names_its_source()
    {
        var server = StubServer.Healthy();

        var result = await TestCli.Run(["status"], server, new Dictionary<string, string> { ["SUWAGCMS_URL"] = "ftp://cms" });

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("SUWAGCMS_URL", result.Error, StringComparison.Ordinal);
        Assert.Empty(server.Requests);
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-13")]
    public async Task Saving_an_address_that_is_not_http_exits_2_and_saves_nothing()
    {
        var folder = TestCli.FreshSettingsFolder();

        var result = await TestCli.Run(["server", "not-an-address"], settingsFolder: folder);

        Assert.Equal(2, result.ExitCode);
        Assert.False(Directory.Exists(folder));
    }
}
