using System.Globalization;
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

    // Why: no entry has an id this low, so an upper bound of it lists nothing, for a version older than history.
    private const long NothingListed = 0;

    private const string Listed = """
        FROM change_entry
        WHERE bot_id = @bot
          AND (@kind::text IS NULL OR (kind = @kind AND (public_id = @id OR resource_id IN (
              SELECT resource_id FROM change_entry WHERE bot_id = @bot AND kind = @kind AND public_id = @id
          ))))
          AND (@after::bigint IS NULL OR id > @after)
          AND (@through::bigint IS NULL OR id <= @through)
        """;

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

    // Why: what one read lists; After and Through are ids, since a window goes by the order entries were recorded and two can share a time.
    private sealed record Listing(string Bot, string? Kind, string? Id, long? After, long? Through);

    private sealed record CreatedRow(string Number, long Id);

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

    // Why: one row more than the page says whether another page exists without a second query, and only a numbered page pays for a count; null means the bot is unknown.
    public static async Task<HistoryPage?> ReadAsync(
        Pool pool,
        string bot,
        (string Kind, string Id)? resource,
        int limit,
        long? before,
        int? page,
        CancellationToken cancellationToken
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        if (!await ResourceStore.BotExistsAsync(connection, null, bot, cancellationToken))
        {
            return null;
        }
        return await PageAsync(
            connection,
            new Listing(bot, resource?.Kind, resource?.Id, null, null),
            limit,
            before,
            page,
            null,
            cancellationToken
        );
    }

    // Why: the entries from the previous version's creation, not counting it, through this version's own, found by their ids; a version older than history has no such entry, so nothing is listed.
    public static async Task<HistoryPage> ReadWindowAsync(
        Pool pool,
        string bot,
        int number,
        int limit,
        long? before,
        int? page,
        CancellationToken cancellationToken
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var own = number.ToString(CultureInfo.InvariantCulture);
        var previous = (number - 1).ToString(CultureInfo.InvariantCulture);
        var created = (
            await connection.QueryAsync<CreatedRow>(
                new CommandDefinition(
                    """
                    SELECT public_id AS Number, id AS Id FROM change_entry
                    WHERE bot_id = @bot AND kind = @kind AND action = 'created' AND public_id IN (@own, @previous)
                    """,
                    new
                    {
                        bot,
                        kind = VersionKind,
                        own,
                        previous,
                    },
                    cancellationToken: cancellationToken
                )
            )
        ).ToDictionary(row => row.Number, row => row.Id, StringComparer.Ordinal);
        var through = created.TryGetValue(own, out var ownEntry) ? ownEntry : NothingListed;
        long? after = created.TryGetValue(previous, out var previousEntry) ? previousEntry : null;
        var incomplete =
            through == NothingListed
            || (number == 1 ? await RegisteredBeforeHistoryAsync(connection, bot, cancellationToken) : after is null);
        return await PageAsync(
            connection,
            new Listing(bot, null, null, after, through),
            limit,
            before,
            page,
            incomplete,
            cancellationToken
        );
    }

    private static async Task<bool> RegisteredBeforeHistoryAsync(
        NpgsqlConnection connection,
        string bot,
        CancellationToken cancellationToken
    ) =>
        await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT created_at < (SELECT min(started_at) FROM history_start) FROM bot WHERE id = @bot",
                new { bot },
                cancellationToken: cancellationToken
            )
        );

    private static async Task<HistoryPage> PageAsync(
        NpgsqlConnection connection,
        Listing listing,
        int limit,
        long? before,
        int? page,
        bool? incomplete,
        CancellationToken cancellationToken
    )
    {
        var rows = (
            await connection.QueryAsync<EntryRow>(
                new CommandDefinition(
                    $"""
                    SELECT id AS Id, at AS At, kind AS Kind, public_id AS PublicId, action AS Action, revision AS Revision, changes::text AS Changes, batch AS Batch
                    {Listed}
                      AND (@before::bigint IS NULL OR id < @before)
                    ORDER BY id DESC
                    LIMIT @size OFFSET @skip
                    """,
                    new
                    {
                        bot = listing.Bot,
                        kind = listing.Kind,
                        id = listing.Id,
                        after = listing.After,
                        through = listing.Through,
                        before,
                        size = limit + 1,
                        skip = (Math.Max(page ?? 1, 1) - 1L) * limit,
                    },
                    cancellationToken: cancellationToken
                )
            )
        ).ToList();
        long? total = page is null
            ? null
            : await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(
                    $"SELECT count(*) {Listed}",
                    new
                    {
                        bot = listing.Bot,
                        kind = listing.Kind,
                        id = listing.Id,
                        after = listing.After,
                        through = listing.Through,
                    },
                    cancellationToken: cancellationToken
                )
            );
        var since = await connection.ExecuteScalarAsync<DateTime>(
            new CommandDefinition("SELECT min(started_at) FROM history_start", cancellationToken: cancellationToken)
        );
        var shown = rows.Take(limit).ToList();
        return new HistoryPage(
            [.. shown.Select(Shown)],
            rows.Count > limit ? shown[^1].Id : null,
            new DateTimeOffset(DateTime.SpecifyKind(since, DateTimeKind.Utc)),
            total,
            incomplete
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
