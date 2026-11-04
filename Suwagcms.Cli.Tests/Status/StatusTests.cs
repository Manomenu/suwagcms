using System.Net;

namespace Suwagcms.Cli.Tests.Status;

public sealed class StatusTests
{
    [Fact]
    [Trait("criterion", "AC-cli-foundation-3")]
    public async Task Status_prints_the_address_used_and_that_the_server_answers_and_exits_0()
    {
        var server = StubServer.Healthy();

        var result = await TestCli.Run(["status", "--server", "http://cms.internal:3240/"], server);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Server http://cms.internal:3240 reachable" + Environment.NewLine, result.Output);
        Assert.Equal("", result.Error);
        Assert.Equal("http://cms.internal:3240/api/health", Assert.Single(server.Requests).ToString());
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-4")]
    public async Task A_server_that_is_down_exits_3_and_names_the_address_and_how_to_set_another()
    {
        var result = await TestCli.Run(["status", "--server", "http://cms.internal:3240"], StubServer.Down());

        Assert.Equal(3, result.ExitCode);
        Assert.Equal("", result.Output);
        Assert.Contains("http://cms.internal:3240", result.Error, StringComparison.Ordinal);
        Assert.Contains("--server", result.Error, StringComparison.Ordinal);
        Assert.Contains("SUWAGCMS_URL", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-4")]
    public async Task A_server_that_answers_with_a_failure_exits_3_and_repeats_what_the_server_says_to_do()
    {
        var down = StubServer.Answering(
            HttpStatusCode.ServiceUnavailable,
            """{"detail":"The database does not answer: start it with `just db up`."}"""
        );

        var result = await TestCli.Run(["status", "--server", "http://cms.internal"], down);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("http://cms.internal", result.Error, StringComparison.Ordinal);
        Assert.Contains("503", result.Error, StringComparison.Ordinal);
        Assert.Contains("just db up", result.Error, StringComparison.Ordinal);
    }
}
