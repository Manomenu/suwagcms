using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Suwbackkit.Testing;

namespace Suwagcms.Server.Tests.Health;

public sealed class HealthEndpointsTests(TestDatabase database, ClassServer server) : IClassFixture<ClassServer>
{
    private TestApp App => server.App;

    [Fact]
    [Trait("criterion", "AC-cli-foundation-14")]
    public async Task Health_is_ok_when_the_database_answers()
    {
        _ = database.DataSource;
        using var client = App.CreateClient();

        var response = await client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-cli-foundation-14")]
    public async Task Health_says_what_to_do_when_the_database_does_not_answer_and_names_the_trace_in_the_log()
    {
        _ = database.DataSource;
        using var logs = new CapturedLogs();
        var unreachable = new NpgsqlConnectionStringBuilder(TestDatabase.ConnectionString) { Port = 1 }.ConnectionString;
        using var broken = App.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton(NpgsqlDataSource.Create(unreachable)).AddSingleton<ILoggerProvider>(logs)
            )
        );
        using var client = broken.CreateClient();

        var response = await client.GetAsync("/api/health", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var traceId = JsonDocument.Parse(body).RootElement.GetProperty("traceId").GetString();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("The database does not answer: start it, or fix ConnectionStrings:Default.", body, StringComparison.Ordinal);
        Assert.Matches("^[0-9a-f]{32}$", traceId);
        Assert.Contains(logs.Entries, entry => entry.Level >= LogLevel.Warning && entry.TraceId == traceId);
    }
}
