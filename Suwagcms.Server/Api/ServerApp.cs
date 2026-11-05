using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization;
using Suwagcms.Server.Actions;
using Suwagcms.Server.Articles;
using Suwagcms.Server.Availability;
using Suwagcms.Server.Bots;
using Suwagcms.Server.Changes;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;
using Suwagcms.Server.Facts;
using Suwagcms.Server.Health;
using Suwagcms.Server.History;
using Suwagcms.Server.KnowledgeBases;
using Suwagcms.Server.ProcedureLayouts;
using Suwagcms.Server.Procedures;
using Suwagcms.Server.Renames;
using Suwagcms.Server.Usage;
using Suwagcms.Server.Validation;
using Suwagcms.Server.Versions;

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
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<DefinitionEditor>();
        builder.Services.AddSingleton<VersionCreator>();
        builder.Services.AddOptions<PushOptions>().BindConfiguration("Push");
        builder.Services.AddHttpClient<BotLine>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        builder.Services.AddTransient<DeclarationAsker>();
        builder.Services.AddTransient<AvailabilitySender>();
        builder.Services.AddTransient<VersionPusher>();
        builder.Services.AddTransient<ActionTester>();
        builder.Services.AddTransient<BotStatusReader>();
        builder.Services.AddOpenApi();

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        var api = app.MapGroup("/api");
        api.MapHealth();
        api.MapBots();
        api.MapValidation();
        api.MapArticles();
        api.MapFacts();
        api.MapKnowledgeBases();
        api.MapProcedures();
        api.MapActions();
        api.MapVersions();
        api.MapUsage();
        api.MapRenames();
        api.MapProcedureLayouts();
        api.MapChanges();
        api.MapHistory();
        api.MapAvailability();
        return app;
    }
}
