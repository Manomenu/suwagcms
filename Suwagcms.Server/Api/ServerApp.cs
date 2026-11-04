using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization;
using Suwagcms.Server.Bots;
using Suwagcms.Server.Database;
using Suwagcms.Server.Health;

namespace Suwagcms.Server.Api;

internal static class ServerApp
{
    // Why: the OpenAPI build step starts the app only to read its routes, so it must not need a database
    private static bool WritingOpenApiDocument => Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddDatabase(builder.Configuration, migrateOnStart: !WritingOpenApiDocument);
        builder.Services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier
        );
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddOpenApi();

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        var api = app.MapGroup("/api");
        api.MapHealth();
        api.MapBots();
        return app;
    }
}
