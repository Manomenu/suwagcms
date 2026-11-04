using Dapper;
using Npgsql;
using Suwagcms.Sdk.Format;
using Suwagcms.Server.Database;

namespace Suwagcms.Server.Editing;

internal static class ResourceStore
{
    private sealed record Row(Guid Id, string Kind, string PublicId, int Revision, string Body);

    public static async Task LockBotAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string bot,
        CancellationToken cancellationToken
    ) =>
        await connection.ExecuteAsync(
            new CommandDefinition(
                "SELECT pg_advisory_xact_lock(hashtextextended(@bot, 0))",
                new { bot },
                transaction,
                cancellationToken: cancellationToken
            )
        );

    public static async Task<bool> BotExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string bot,
        CancellationToken cancellationToken
    ) =>
        await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM bot WHERE id = @bot)",
                new { bot },
                transaction,
                cancellationToken: cancellationToken
            )
        );

    public static async Task<List<StoredResource>> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string bot,
        CancellationToken cancellationToken
    )
    {
        var rows = await connection.QueryAsync<Row>(
            new CommandDefinition(
                """
                SELECT id AS Id, kind AS Kind, public_id AS PublicId, revision AS Revision, body::text AS Body
                FROM resource WHERE bot_id = @bot ORDER BY kind COLLATE "C", public_id COLLATE "C"
                """,
                new { bot },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        return
        [
            .. rows.Select(row =>
            {
                var kind = ResourceKinds.FromColumn(row.Kind);
                return new StoredResource(row.Id, new ResourceRef(kind, row.PublicId), row.Revision, ResourceKinds.Read(kind, row.Body));
            }),
        ];
    }

    // Why: a read outside the write path needs no lock, since a save commits all its rows at once; null means the bot is unknown.
    public static async Task<List<StoredResource>?> ReadAsync(Pool pool, string bot, CancellationToken cancellationToken)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await BotExistsAsync(connection, null, bot, cancellationToken)
            ? await LoadAsync(connection, null, bot, cancellationToken)
            : null;
    }

    public static async Task InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string bot,
        StoredResource resource,
        DateTimeOffset now,
        CancellationToken cancellationToken
    ) =>
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO resource (id, bot_id, kind, public_id, revision, body, updated_at)
                VALUES (@Id, @bot, @kind, @publicId, @Revision, @body::jsonb, @now)
                """,
                new
                {
                    resource.Id,
                    bot,
                    kind = ResourceKinds.Column(resource.Target.Kind),
                    publicId = resource.Target.Id,
                    resource.Revision,
                    body = ResourceKinds.Write(resource.Record),
                    now,
                },
                transaction,
                cancellationToken: cancellationToken
            )
        );

    public static async Task UpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        StoredResource resource,
        DateTimeOffset now,
        CancellationToken cancellationToken
    ) =>
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE resource SET revision = @Revision, body = @body::jsonb, updated_at = @now WHERE id = @Id",
                new
                {
                    resource.Id,
                    resource.Revision,
                    body = ResourceKinds.Write(resource.Record),
                    now,
                },
                transaction,
                cancellationToken: cancellationToken
            )
        );

    public static async Task DeleteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid id,
        CancellationToken cancellationToken
    ) =>
        await connection.ExecuteAsync(
            new CommandDefinition("DELETE FROM resource WHERE id = @id", new { id }, transaction, cancellationToken: cancellationToken)
        );
}
