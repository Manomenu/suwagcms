using Npgsql;
using Suwbackkit.Postgres;
using Suwbackkit.Testing;

[assembly: AssemblyFixture(typeof(Suwagcms.Server.Tests.TestDatabase))]

namespace Suwagcms.Server.Tests;

public sealed class TestDatabase : IAsyncLifetime
{
    private const string MainCheckoutConnectionString =
        "Host=127.0.0.1;Port=5563;Database=suwagcms_test;Username=suwagcms;Password=suwagcms";

    private static readonly string? FromEnvironment = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING");

    public static readonly string ConnectionString = FromEnvironment ?? MainCheckoutConnectionString;

    private string? unavailable;
    private NpgsqlDataSource? dataSource;

    internal NpgsqlDataSource DataSource
    {
        get
        {
            if (unavailable is not null)
            {
                Assert.Skip($"PostgreSQL unavailable — start it with `just db up` ({unavailable})");
            }
            return dataSource ?? throw new InvalidOperationException("the test database is not initialized");
        }
    }

    public async ValueTask InitializeAsync()
    {
        LinkedWorktree.RefuseMainCheckoutDatabase(FromEnvironment, AppContext.BaseDirectory);
        try
        {
            await TestDatabases.RecreateAsync(ConnectionString);
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException && Environment.GetEnvironmentVariable("CI") is null)
        {
            unavailable = error.Message;
            return;
        }
        dataSource = NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(ConnectionString) { MaxPoolSize = 4 }.ConnectionString);
        await Migrator.MigrateAsync(ConnectionString, Migrator.DefaultDirectory);
    }

    public async ValueTask DisposeAsync()
    {
        if (dataSource is not null)
        {
            await dataSource.DisposeAsync();
        }
    }
}
