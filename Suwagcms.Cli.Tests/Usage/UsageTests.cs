using System.Text.Json;

namespace Suwagcms.Cli.Tests.Usage;

public sealed class UsageTests
{
    [Fact]
    [Trait("criterion", "AC-cli-foundation-8")]
    public async Task An_unknown_command_exits_2_and_prints_the_root_usage()
    {
        var result = await TestCli.Run(["frobnicate"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("", result.Output);
        Assert.Contains("frobnicate", result.Error, StringComparison.Ordinal);
        Assert.Contains("Usage:", result.Error, StringComparison.Ordinal);
        Assert.Contains("status", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-8")]
    public async Task An_unknown_option_exits_2_and_prints_the_usage_of_its_command()
    {
        var result = await TestCli.Run(["status", "--colour"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("--colour", result.Error, StringComparison.Ordinal);
        Assert.Contains("Usage:", result.Error, StringComparison.Ordinal);
        Assert.Contains("status [options]", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-8")]
    public async Task A_missing_argument_exits_2_and_prints_the_usage_of_its_command()
    {
        var result = await TestCli.Run(["server"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Required argument missing", result.Error, StringComparison.Ordinal);
        Assert.Contains("server <url>", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_wrong_use_with_json_prints_one_document_with_the_error_and_the_usage()
    {
        var result = await TestCli.Run(["server", "--json"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Equal("", result.Error);
        using var document = JsonDocument.Parse(result.Output);
        Assert.Contains("Required argument missing", document.RootElement.GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.Contains("server <url>", document.RootElement.GetProperty("usage").GetString(), StringComparison.Ordinal);
    }
}
