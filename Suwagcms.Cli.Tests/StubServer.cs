using System.Net;
using System.Text;

namespace Suwagcms.Cli.Tests;

internal sealed class StubServer(Func<HttpRequestMessage, HttpResponseMessage> answer)
{
    public List<Uri> Requests { get; } = [];

    public static StubServer Healthy() => new(_ => new HttpResponseMessage(HttpStatusCode.OK));

    public static StubServer Down() => new(_ => throw new HttpRequestException("Connection refused"));

    public static StubServer Answering(HttpStatusCode status, string json) =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/problem+json") });

    public HttpResponseMessage Answer(HttpRequestMessage request)
    {
        Requests.Add(request.RequestUri ?? throw new InvalidOperationException("a request without an address"));
        return answer(request);
    }
}
