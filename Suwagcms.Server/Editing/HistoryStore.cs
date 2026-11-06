using System.Text.Json;
using Dapper;
using Npgsql;
using Suwagcms.Contracts.History;
using Suwagcms.Server.Database;

namespace Suwagcms.Server.Editing;

// Why: an entry is written on the connection and in the transaction of the change it records, so a change can never exist unrecorded, and this store has no statement that alters an entry.
internal static class HistoryStore
{
    public const string VersionKind = "version";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    internal sealed record Recorded(
        string Kind,
        string Id,
        string Action,
        int? Revision,
        IReadOnlyList<FieldChange> Changes,
        Guid? Batch = null,
        Guid? Resource = null
    );

    private sealed record EntryRow(
        long Id,
        DateTime At,
        string Kind,
        string PublicId,
        string Action,
        int? Revision,
        string Changes,
        Guid? Batch
    );

    public static async Task WriteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string bot,
        Recorded entry,
        DateTimeOffset at,
        CancellationToken cancellationToken
    ) =>
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO change_entry (bot_id, kind, public_id, action, revision, changes, batch, resource_id, at)
                VALUES (@bot, @Kind, @Id, @Action, @Revision, @changes::jsonb, @Batch, @Resource, @at)
                """,
                new
                {
                    bot,
                    entry.Kind,
                    entry.Id,
                    entry.Action,
                    entry.Revision,
                    changes = JsonSerializer.Serialize(entry.Changes, Web),
                    entry.Batch,
                    entry.Resource,
                    at,
                },
                transaction,
                cancellationToken: cancellationToken
            )
        );

    // Why: one row more than the page says whether another page exists without a second query; null means the bot is unknown.
    public static async Task<HistoryPage?> ReadAsync(
        Pool pool,
        string bot,
        (string Kind, string Id)? resource,
        int limit,
        long? before,
        CancellationToken cancellationToken
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        if (!await ResourceStore.BotExistsAsync(connection, null, bot, cancellationToken))
        {
            return null;
        }
        var rows = (
            await connection.QueryAsync<EntryRow>(
                new CommandDefinition(
                    """
                    SELECT id AS Id, at AS At, kind AS Kind, public_id AS PublicId, action AS Action, revision AS Revision, changes::text AS Changes, batch AS Batch
                    FROM change_entry
                    WHERE bot_id = @bot
                      AND (@kind::text IS NULL OR (kind = @kind AND (public_id = @id OR resource_id IN (
                          SELECT resource_id FROM change_entry WHERE bot_id = @bot AND kind = @kind AND public_id = @id
                      ))))
                      AND (@before::bigint IS NULL OR id < @before)
                    ORDER BY id DESC
                    LIMIT @size
                    """,
                    new
                    {
                        bot,
                        kind = resource?.Kind,
                        id = resource?.Id,
                        before,
                        size = limit + 1,
                    },
                    cancellationToken: cancellationToken
                )
            )
        ).ToList();
        var since = await connection.ExecuteScalarAsync<DateTime>(
            new CommandDefinition("SELECT min(started_at) FROM history_start", cancellationToken: cancellationToken)
        );
        var page = rows.Take(limit).ToList();
        return new HistoryPage(
            [.. page.Select(Shown)],
            rows.Count > limit ? page[^1].Id : null,
            new DateTimeOffset(DateTime.SpecifyKind(since, DateTimeKind.Utc))
        );
    }

    private static HistoryEntry Shown(EntryRow row) =>
        new(
            row.Id,
            new DateTimeOffset(DateTime.SpecifyKind(row.At, DateTimeKind.Utc)),
            row.Kind,
            row.PublicId,
            row.Action,
            row.Revision,
            JsonSerializer.Deserialize<List<FieldChange>>(row.Changes, Web) ?? [],
            row.Batch
        );
}
