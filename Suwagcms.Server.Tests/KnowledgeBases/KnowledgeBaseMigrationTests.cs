using System.Text.Json.Nodes;
using Dapper;
using Npgsql;
using Suwagcms.Server.Database;

namespace Suwagcms.Server.Tests.KnowledgeBases;

public sealed class KnowledgeBaseMigrationTests(TestDatabase database) : IAsyncLifetime
{
    private const string Migration = "0015_knowledge_base_search_fields.sql";

    private readonly string connectionString = new NpgsqlConnectionStringBuilder(TestDatabase.ConnectionString)
    {
        Database = $"kb_migration_{Guid.NewGuid():N}",
    }.ConnectionString;

    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("kb-migration-");

    public async ValueTask InitializeAsync()
    {
        _ = database.Pool;
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

    private static JsonObject Body(string json) =>
        JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("a stored body is null");

    [Fact]
    [Trait("criterion", "AC-knowledge-bases-16")]
    public async Task Every_base_stored_before_it_gets_default_and_5_and_keeps_its_fields_and_nothing_else_changes()
    {
        CopyMigrations(name => string.CompareOrdinal(name, Migration) < 0);
        await Migrator.MigrateAsync(connectionString, directory.FullName);
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO bot (id, name, address) VALUES ('old', 'Old', 'http://old.internal');
                INSERT INTO resource (id, bot_id, kind, public_id, revision, body, updated_at) VALUES
                    (gen_random_uuid(), 'old', 'knowledgeBase', 'offer', 4, '{"id":"offer","title":"Our offer","language":"pl"}', now()),
                    (gen_random_uuid(), 'old', 'globalFact', 'customer.city', 1, '{"name":"customer.city","type":"text","description":"d","maxLength":5}', now());
                """
            );
        }

        CopyMigrations(name => name == Migration);
        await Migrator.MigrateAsync(connectionString, directory.FullName);

        await using var reading = new NpgsqlConnection(connectionString);
        var rows = (
            await reading.QueryAsync<(string Kind, int Revision, string Body)>(
                "SELECT kind, revision, body::text FROM resource ORDER BY kind"
            )
        ).ToList();
        var fact = Body(rows[0].Body);
        var knowledgeBase = Body(rows[1].Body);
        Assert.Equal(
            ("offer", "Our offer", "pl", "default", 5, 4),
            (
                knowledgeBase["id"]?.GetValue<string>(),
                knowledgeBase["title"]?.GetValue<string>(),
                knowledgeBase["language"]?.GetValue<string>(),
                knowledgeBase["embedding"]?.GetValue<string>(),
                knowledgeBase["results"]?.GetValue<int>(),
                rows[1].Revision
            )
        );
        Assert.Equal(["name", "type", "description", "maxLength"], fact.Select(field => field.Key));
    }
}
