using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Suwagcms.Sdk.Tests.BotRuntime.Actions;

internal sealed class TestService : IAsyncDisposable
{
    internal sealed record Received(string Method, string PathAndQuery, string Body, Dictionary<string, string> Headers);

    private readonly WebApplication app;

    private TestService(WebApplication app)
    {
        this.app = app;
    }

    public List<Received> Requests { get; } = [];

    public Func<HttpContext, Task> Answer { get; set; } = context => context.Response.WriteAsync("{}");

    public string Address => app.Urls.Single();

    public static async Task<TestService> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var service = new TestService(app);
        app.Run(async context =>
        {
            using var reader = new StreamReader(context.Request.Body);
            service.Requests.Add(
                new Received(
                    context.Request.Method,
                    context.Request.Path + context.Request.QueryString,
                    await reader.ReadToEndAsync(context.RequestAborted),
                    context.Request.Headers.ToDictionary(
                        header => header.Key,
                        header => header.Value.ToString(),
                        StringComparer.OrdinalIgnoreCase
                    )
                )
            );
            await service.Answer(context);
        });
        await app.StartAsync();
        return service;
    }

    public async ValueTask DisposeAsync()
    {
        await app.StopAsync();
        await app.DisposeAsync();
    }
}
