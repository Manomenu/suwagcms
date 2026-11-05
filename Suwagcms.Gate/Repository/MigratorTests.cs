using Npgsql;
using Suwagcms.Server.Database;

namespace Suwagcms.Gate.Repository;

// Why: a migration that already ran is never edited, and grate is what enforces it; this proves the repository's own setup of it still does.
public sealed class MigratorTests : IAsyncLifetime
{
    private static readonly string Base =
        Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING")
        ?? "Host=127.0.0.1;Port=5563;Database=suwagcms_test;Username=suwagcms;Password=suwagcms";

    private readonly string connectionString = new NpgsqlConnectionStringBuilder(Base)
    {
        Database = $"migrator_probe_{Guid.NewGuid():N}",
    }.ConnectionString;
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("migrations-");
    private string? unavailable;

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(Path.Combine(directory.FullName, "up"));
        try
        {
            await AdminAsync($"CREATE DATABASE \"{new NpgsqlConnectionStringBuilder(connectionString).Database}\"");
        }
        catch (Exception error) when (error is NpgsqlException or TimeoutException && Environment.GetEnvironmentVariable("CI") is null)
        {
            unavailable = error.Message;
        }
    }

    public async ValueTask DisposeAsync()
    {
        directory.Delete(recursive: true);
        if (unavailable is null)
        {
            NpgsqlConnection.ClearAllPools();
            await AdminAsync($"DROP DATABASE IF EXISTS \"{new NpgsqlConnectionStringBuilder(connectionString).Database}\" WITH (FORCE)");
        }
    }

    [Fact]
    public async Task A_new_script_runs_once_and_only_once()
    {
        SkipWithoutDatabase();
        Write("0001_counter.sql", "CREATE TABLE counter (x int); INSERT INTO counter VALUES (1);");

        await Migrator.MigrateAsync(connectionString, directory.FullName);
        await Migrator.MigrateAsync(connectionString, directory.FullName);

        Assert.True(
            await ScalarAsync("SELECT count(*) FROM counter") is 1L,
            "a script ran more than once: check the grate setup in Migrator.cs"
        );
    }

    [Fact]
    public async Task A_script_edited_after_it_ran_fails_the_migration()
    {
        SkipWithoutDatabase();
        Write("0001_counter.sql", "CREATE TABLE IF NOT EXISTS counter (x int);");
        await Migrator.MigrateAsync(connectionString, directory.FullName);
        Write("0001_counter.sql", "CREATE TABLE IF NOT EXISTS counter (x int, y int);");

        var error = await Assert.ThrowsAnyAsync<Exception>(() => Migrator.MigrateAsync(connectionString, directory.FullName));

        Assert.Contains("0001_counter.sql", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failing_script_leaves_nothing_behind()
    {
        SkipWithoutDatabase();
        Write("0001_fine.sql", "CREATE TABLE fine (x int);");
        Write("0002_broken.sql", "CREATE TABLE broken (x int); SELECT * FROM does_not_exist;");

        await Assert.ThrowsAnyAsync<Exception>(() => Migrator.MigrateAsync(connectionString, directory.FullName));

        Assert.True(
            await ScalarAsync("SELECT count(*) FROM pg_tables WHERE tablename IN ('fine', 'broken')") is 0L,
            "a failed migration left tables behind: keep WithTransaction in Migrator.cs"
        );
    }

    [Fact]
    public async Task A_migration_leaves_no_temporary_folders_of_grate_behind()
    {
        SkipWithoutDatabase();
        Write("0001_counter.sql", "CREATE TABLE counter (x int);");

        var folders = await Migrator.MigrateAsync(connectionString, directory.FullName);

        Assert.NotEmpty(folders);
        Assert.All(folders, folder => Assert.False(Directory.Exists(folder), $"delete grate's temporary folder {folder} in Migrator.cs"));
        Assert.True(Directory.Exists(directory.FullName));
    }

    private void SkipWithoutDatabase()
    {
        if (unavailable is not null)
        {
            Assert.Skip($"PostgreSQL unavailable — start it with `just db up` ({unavailable})");
        }
    }

    private void Write(string name, string sql) => File.WriteAllText(Path.Combine(directory.FullName, "up", name), sql);

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private static async Task AdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(
            new NpgsqlConnectionStringBuilder(Base) { Database = "postgres" }.ConnectionString
        );
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
