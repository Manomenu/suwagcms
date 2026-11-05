using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Suwagcms.Sdk.BotRuntime;

namespace Suwagcms.Server.Tests.Versions;

// Why: a bot built only on the SDK (its setup call, its route call and a store), listening on a real port, so the CMS reaches it as it reaches any bot.
internal sealed class TestBot : IAsyncDisposable
{
    private Func<Task>? stop;
    private IServiceProvider? services;

    private TestBot(string id, IVersionStore store, int port)
    {
        Id = id;
        Store = store;
        Port = port;
    }

    public string Id { get; }

    public IVersionStore Store { get; }

    public int Port { get; }

    public string Address => $"http://127.0.0.1:{Port}";

    // Why: lets a test hold, slow or change the push that reaches the bot, which the bot's own routes cannot do.
    public Func<HttpContext, Task>? OnPush { get; set; }

    public IBotDefinitions Definitions =>
        (services ?? throw new InvalidOperationException("the test bot is stopped")).GetRequiredService<IBotDefinitions>();

    public static async Task<TestBot> StartAsync(string id, IVersionStore? store = null, int? port = null)
    {
        var bot = new TestBot(id, store ?? new InMemoryVersionStore(), port ?? FreePort());
        await bot.StartAsync();
        return bot;
    }

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(Address);
        builder.Logging.ClearProviders();
        builder.Services.AddSuwagcmsBot(Id, Store, TimeSpan.FromHours(1));
        var started = builder.Build();
        started.Use(
            async (context, next) =>
            {
                if (HttpMethods.IsPut(context.Request.Method) && OnPush is { } hook)
                {
                    await hook(context);
                    if (context.RequestAborted.IsCancellationRequested)
                    {
                        return;
                    }
                }
                await next(context);
            }
        );
        started.MapSuwagcmsBot();
        await started.StartAsync(TestContext.Current.CancellationToken);
        services = started.Services;
        stop = async () =>
        {
            await started.StopAsync(CancellationToken.None);
            await started.DisposeAsync();
        };
    }

    public async Task StopAsync()
    {
        if (stop is { } running)
        {
            stop = null;
            services = null;
            await running();
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    // Why: a request sent straight to the bot, as another process would, without the CMS knowing of it.
    public static async Task<HttpStatusCode> PutDirectAsync(string address, int number, string document)
    {
        using var http = new HttpClient();
        using var content = new StringContent(document, System.Text.Encoding.UTF8, "application/json");
        using var response = await http.PutAsync(
            new Uri($"{address}/suwagcms/versions/{number}"),
            content,
            TestContext.Current.CancellationToken
        );
        return response.StatusCode;
    }

    public static async Task<string> StateAsync(string address)
    {
        using var http = new HttpClient();
        return await http.GetStringAsync(new Uri($"{address}/suwagcms/state"), TestContext.Current.CancellationToken);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
