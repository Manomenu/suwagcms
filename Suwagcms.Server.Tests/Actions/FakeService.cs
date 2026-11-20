using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Suwagcms.Server.Tests.Actions;

internal sealed class FakeService
{
    private readonly WebApplication app;
    private bool stopped;

    private FakeService(WebApplication app)
    {
        this.app = app;
    }

    public List<(string Authorization, string Body)> Received { get; } = [];

    public Func<HttpContext, Task> Answer { get; set; } = context => context.Response.WriteAsync("""{"serviceable":"yes"}""");

    public string Address => app.Urls.Single();

    public static async Task<FakeService> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var service = new FakeService(app);
        app.Run(async context =>
        {
            using var reader = new StreamReader(context.Request.Body);
            service.Received.Add((context.Request.Headers.Authorization.ToString(), await reader.ReadToEndAsync(context.RequestAborted)));
            await service.Answer(context);
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        return service;
    }

    public async Task StopAsync()
    {
        if (!stopped)
        {
            stopped = true;
            await app.StopAsync(CancellationToken.None);
            await app.DisposeAsync();
        }
    }
}
