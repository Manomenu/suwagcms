using Dapper;
using Npgsql;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;

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

    public static async Task<BotDeclaration?> DeclaredAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string bot,
        CancellationToken cancellationToken
    )
    {
        var text = await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(
                "SELECT declaration FROM bot WHERE id = @bot",
                new { bot },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        return text is null ? null : DeclarationValidator.ParseJson(text).Declaration;
    }

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

    public static async Task<List<StoredResource>?> ReadAsync(NpgsqlDataSource dataSource, string bot, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
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
                VALUES (@Id, @bot, @kind, @publicId, @Revision, @body::json, @now)
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
                "UPDATE resource SET public_id = @publicId, revision = @Revision, body = @body::json, updated_at = @now WHERE id = @Id",
                new
                {
                    resource.Id,
                    publicId = resource.Target.Id,
                    resource.Revision,
                    body = ResourceKinds.Write(resource.Record),
                    now,
                },
                transaction,
                cancellationToken: cancellationToken
            )
        );

    public static async Task RememberFormerIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string bot,
        StoredResource resource,
        CancellationToken cancellationToken
    ) =>
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO resource_former_id (resource_id, public_id, up_to_version)
                SELECT @Id, @publicId, max(number) FROM bot_version WHERE bot_id = @bot HAVING max(number) IS NOT NULL
                ON CONFLICT (resource_id, public_id) DO UPDATE SET up_to_version = EXCLUDED.up_to_version
                """,
                new
                {
                    resource.Id,
                    publicId = resource.Target.Id,
                    bot,
                },
                transaction,
                cancellationToken: cancellationToken
            )
        );

    public static async Task CountAvailabilityChangeAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string bot,
        Guid id,
        CancellationToken cancellationToken
    ) =>
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE bot SET availability_number = availability_number + 1
                WHERE id = @bot AND EXISTS (SELECT 1 FROM availability WHERE resource_id = @id)
                """,
                new { bot, id },
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
