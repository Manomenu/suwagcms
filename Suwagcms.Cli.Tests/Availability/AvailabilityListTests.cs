using System.Net;

namespace Suwagcms.Cli.Tests.Availability;

public sealed class AvailabilityListTests
{
    private const string Listed = """
        {"number":3,"resources":[{"kind":"article","id":"cennik","off":true}],
        "delivery":{"state":"NotAsked","number":3,"botNumber":null,"reason":null}}
        """;

    [Fact]
    [Trait("criterion", "AC-cli-foundation-20")]
    public async Task Stored_lists_what_the_cms_holds_without_asking_the_bot_and_says_so()
    {
        var server = StubServer.Answering(HttpStatusCode.OK, Listed);

        var result = await TestCli.Run(
            ["availability", "list", "--stored", "--server", "http://cms.internal", "--bot", "rzeczoznawca"],
            server
        );

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("http://cms.internal/api/bots/rzeczoznawca/availability?ask=false", server.Requests[0].AbsoluteUri);
        Assert.Contains("article cennik: off", result.Output, StringComparison.Ordinal);
        Assert.Contains("Bot rzeczoznawca was not asked", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-20")]
    public async Task Without_stored_the_bot_is_asked()
    {
        var server = StubServer.Answering(HttpStatusCode.OK, Listed);

        await TestCli.Run(["availability", "list", "--server", "http://cms.internal", "--bot", "rzeczoznawca"], server);

        Assert.Equal("http://cms.internal/api/bots/rzeczoznawca/availability", server.Requests[0].AbsoluteUri);
    }
}
