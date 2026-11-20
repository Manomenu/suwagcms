using System.Text.Json.Serialization;
using Suwagcms.Server.Actions;
using Suwagcms.Server.Articles;
using Suwagcms.Server.Availability;
using Suwagcms.Server.Bots;
using Suwagcms.Server.Changes;
using Suwagcms.Server.Editing;
using Suwagcms.Server.Facts;
using Suwagcms.Server.History;
using Suwagcms.Server.KnowledgeBases;
using Suwagcms.Server.Personas;
using Suwagcms.Server.Procedures;
using Suwagcms.Server.Renames;
using Suwagcms.Server.Settings;
using Suwagcms.Server.Usage;
using Suwagcms.Server.Validation;
using Suwagcms.Server.Versions;
using Suwbackkit.Postgres;
using Suwbackkit.Server;

namespace Suwagcms.Server.Api;

internal static class ServerApp
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServerDefaults();
        builder.Services.AddDatabase(builder.Configuration, migrateOnStart: !ServerDefaults.WritingOpenApiDocument);
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<DefinitionEditor>();
        builder.Services.AddSingleton<VersionCreator>();
        builder.Services.AddOptions<PushOptions>().BindConfiguration("Push");
        builder.Services.AddHttpClient<BotClient>(client => client.Timeout = Timeout.InfiniteTimeSpan);
        builder.Services.AddTransient<DeclarationAsker>();
        builder.Services.AddTransient<AvailabilitySender>();
        builder.Services.AddSingleton<AvailabilityRetries>();
        if (!ServerDefaults.WritingOpenApiDocument)
        {
            builder.Services.AddHostedService<AvailabilityRetrier>();
        }
        builder.Services.AddTransient<VersionPusher>();
        builder.Services.AddTransient<ActionTester>();
        builder.Services.AddTransient<BotStatusReader>();

        var app = builder.Build();
        app.MapServerDefaults();

        var api = app.MapGroup("/api");
        api.MapHealthStatus();
        api.MapSettings(PageSize.Read(app.Configuration[PageSize.Variable]));
        api.MapBots();
        api.MapValidation();
        api.MapArticles();
        api.MapFacts();
        api.MapKnowledgeBases();
        api.MapProcedures();
        api.MapActions();
        api.MapPersona();
        api.MapVersions();
        api.MapUsage();
        api.MapRenames();
        api.MapChanges();
        api.MapHistory();
        api.MapAvailability();
        return app;
    }
}
