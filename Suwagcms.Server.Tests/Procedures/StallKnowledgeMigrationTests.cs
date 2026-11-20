using System.Text.Json.Nodes;
using Dapper;
using Npgsql;
using Suwbackkit.Postgres;

namespace Suwagcms.Server.Tests.Procedures;

public sealed class StallKnowledgeMigrationTests(TestDatabase database) : IAsyncLifetime
{
    private const string Migration = "0016_drop_when_stalled_knowledge.sql";

    private readonly string connectionString = new NpgsqlConnectionStringBuilder(TestDatabase.ConnectionString)
    {
        Database = $"stall_migration_{Guid.NewGuid():N}",
    }.ConnectionString;

    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("stall-migration-");

    public async ValueTask InitializeAsync()
    {
        _ = database.DataSource;
        await TestDatabase.RecreateAsync(connectionString);
    }

    public async ValueTask DisposeAsync()
    {
        directory.Delete(recursive: true);
        NpgsqlConnection.ClearAllPools();
        await TestDatabase.DropAsync(connectionString);
    }

    private void CopyMigrations(Func<string, bool> which)
    {
        var up = Directory.CreateDirectory(Path.Combine(directory.FullName, "up"));
        foreach (
            var file in Directory.EnumerateFiles(Path.Combine(Migrator.DefaultDirectory, "up")).Where(file => which(Path.GetFileName(file)))
        )
        {
            File.Copy(file, Path.Combine(up.FullName, Path.GetFileName(file)), overwrite: true);
        }
    }

    [Fact]
    [Trait("criterion", "AC-procedure-creator-50")]
    public async Task Knowledge_on_when_stalled_is_removed_and_the_rest_keeps_its_order_and_revision()
    {
        const string informing =
            """{"id":"tell","description":"d","firstStep":"t","steps":{"t":{"inform":{"instruction":"i","points":{"zeta":"z","alpha":"a"},"knowledge":[{"knowledgeBase":"faq","articles":["x"]}]}}},"whenStalled":{"instruction":"Say so."}}""";
        CopyMigrations(name => string.CompareOrdinal(name, Migration) < 0);
        await Migrator.MigrateAsync(connectionString, directory.FullName);
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO bot (id, name, address) VALUES ('old', 'Old', 'http://old.internal');
                INSERT INTO resource (id, bot_id, kind, public_id, revision, body, updated_at) VALUES
                    (gen_random_uuid(), 'old', 'procedure', 'stalling', 3, '{"id":"stalling","description":"d","whenStalled":{"knowledge":[{"knowledgeBase":"faq","searchQueries":["q"]}],"instruction":"Say so."},"localFacts":{}}', now()),
                    (gen_random_uuid(), 'old', 'procedure', 'tell', 2, @informing::json, now());
                """,
                new { informing }
            );
        }

        CopyMigrations(name => name == Migration);
        await Migrator.MigrateAsync(connectionString, directory.FullName);

        await using var reading = new NpgsqlConnection(connectionString);
        var rows = (
            await reading.QueryAsync<(string Id, int Revision, string Body)>(
                "SELECT public_id, revision, body::text FROM resource ORDER BY public_id"
            )
        ).ToList();
        var stalling = JsonNode.Parse(rows[0].Body)?.AsObject() ?? throw new InvalidOperationException("a stored body is null");
        Assert.Equal(["id", "description", "whenStalled", "localFacts"], stalling.Select(field => field.Key));
        Assert.Equal("""{"instruction":"Say so."}""", stalling["whenStalled"]?.ToJsonString());
        Assert.Equal((3, 2), (rows[0].Revision, rows[1].Revision));
        Assert.Equal(informing, rows[1].Body);
    }
}
