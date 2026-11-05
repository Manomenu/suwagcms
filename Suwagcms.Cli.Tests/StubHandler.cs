namespace Suwagcms.Cli.Tests;

internal sealed class StubHandler(StubServer server) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(server.Answer(request));
}
