using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.BotRuntime;

public static class SuwagcmsBotExtensions
{
    private const string ProblemJson = "application/problem+json";

    public static IServiceCollection AddSuwagcmsBot(
        this IServiceCollection services,
        string botId,
        IVersionStore? store = null,
        TimeSpan? pollInterval = null,
        BotCapabilities? declares = null,
        Func<string, string?>? findSecret = null,
        Func<string, IReadOnlyDictionary<string, string>, CancellationToken, Task<ActionOutcome>>? runCodeAction = null
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(botId);
        services.AddSingleton(BotDeclaration.Of(botId, declares));
        services.AddSingleton(_ => new HttpActionRunner(findSecret));
        services.AddSingleton(provider => new ActionTestRoute(
            provider.GetRequiredService<HttpActionRunner>(),
            runCodeAction,
            TimeProvider.System
        ));
        services.AddSingleton(provider => new VersionRuntime(
            botId,
            store
                ?? new FileVersionStore(Path.Combine(provider.GetRequiredService<IHostEnvironment>().ContentRootPath, "suwagcms-versions")),
            provider.GetRequiredService<BotDeclaration>()
        ));
        services.AddSingleton<IBotDefinitions>(provider => provider.GetRequiredService<VersionRuntime>());
        services.AddHostedService(provider => new StorePoller(
            provider.GetRequiredService<VersionRuntime>(),
            pollInterval ?? TimeSpan.FromSeconds(5)
        ));
        return services;
    }

    public static IEndpointConventionBuilder MapSuwagcmsBot(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = endpoints.MapGroup("/suwagcms");
        group.MapGet(
            "/state",
            async (VersionRuntime runtime, CancellationToken cancellationToken) =>
                Results.Json(await runtime.ReadStateAsync(cancellationToken), BotRuntimeJson.Options)
        );
        group.MapGet("/declaration", (BotDeclaration declaration) => Results.Json(declaration, BotRuntimeJson.Options));
        group.MapPut(
            "/versions/{number:int}",
            async (int number, HttpRequest request, VersionRuntime runtime, CancellationToken cancellationToken) =>
            {
                using var reader = new StreamReader(request.Body, Encoding.UTF8);
                var (accepted, refused) = await runtime.PushAsync(
                    number,
                    await reader.ReadToEndAsync(cancellationToken),
                    cancellationToken
                );
                return refused is null
                    ? Results.Json(accepted, BotRuntimeJson.Options)
                    : Results.Json(refused, BotRuntimeJson.Options, ProblemJson, refused.Status);
            }
        );
        group.MapPost(
            "/actions/test",
            async (HttpRequest request, ActionTestRoute route, CancellationToken cancellationToken) =>
            {
                using var reader = new StreamReader(request.Body, Encoding.UTF8);
                try
                {
                    return Results.Json(
                        await route.RunAsync(await reader.ReadToEndAsync(cancellationToken), cancellationToken),
                        BotRuntimeJson.Options
                    );
                }
                catch (JsonException exception)
                {
                    return Results.Json(
                        new BotRefusal
                        {
                            Title = "The test run was refused.",
                            Status = StatusCodes.Status400BadRequest,
                            Detail = $"The body does not fit the route: {exception.Message}",
                            Mistakes = [],
                        },
                        BotRuntimeJson.Options,
                        ProblemJson,
                        StatusCodes.Status400BadRequest
                    );
                }
            }
        );
        return group;
    }
}
