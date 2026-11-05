using System.Text.Json;

namespace Suwagcms.Cli.Tests.Output;

public sealed class JsonOutputTests
{
    [Fact]
    [Trait("criterion", "AC-cli-foundation-7")]
    public async Task A_successful_status_prints_exactly_one_json_document_and_nothing_else()
    {
        var result = await TestCli.Run(
            ["status", "--json"],
            StubServer.Healthy(),
            new Dictionary<string, string> { ["SUWAGCMS_URL"] = "http://cms.internal" }
        );

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("", result.Error);
        using var document = JsonDocument.Parse(result.Output);
        Assert.Equal("http://cms.internal", document.RootElement.GetProperty("server").GetString());
        Assert.True(document.RootElement.GetProperty("reachable").GetBoolean());
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-7")]
    public async Task A_successful_server_command_prints_exactly_one_json_document_and_nothing_else()
    {
        var folder = TestCli.FreshSettingsFolder();

        var result = await TestCli.Run(["server", "http://cms.internal/", "--json"], settingsFolder: folder);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("", result.Error);
        using var document = JsonDocument.Parse(result.Output);
        Assert.Equal("http://cms.internal", document.RootElement.GetProperty("server").GetString());
    }

    [Fact]
    public async Task A_server_that_is_down_with_json_prints_one_document_holding_the_message()
    {
        var result = await TestCli.Run(["status", "--json", "--server", "http://cms.internal"], StubServer.Down());

        Assert.Equal(3, result.ExitCode);
        Assert.Equal("", result.Error);
        using var document = JsonDocument.Parse(result.Output);
        Assert.Contains("http://cms.internal", document.RootElement.GetProperty("error").GetString(), StringComparison.Ordinal);
    }
}
