using Dapper;
using Npgsql;
using Suwagcms.Contracts;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Versions;

internal static class VersionStore
{
    private sealed record ListedVersion(
        int Number,
        DateTime CreatedAt,
        int Facts,
        int KnowledgeBases,
        int Articles,
        int Procedures,
        string Sha256
    );

    private sealed record StoredVersion(
        string Document,
        DateTime CreatedAt,
        int Facts,
        int KnowledgeBases,
        int Articles,
        int Procedures,
        string Sha256
    );

    internal sealed record Latest(int Number, string Sha256);

    private const string Counts = """
        created_at AS CreatedAt,
        jsonb_array_length(document::jsonb -> 'facts') AS Facts,
        jsonb_array_length(document::jsonb -> 'knowledgeBases') AS KnowledgeBases,
        jsonb_array_length(document::jsonb -> 'articles') AS Articles,
        jsonb_array_length(document::jsonb -> 'procedures') AS Procedures,
        sha256 AS Sha256
        """;

    // Why: a read outside the write path needs no lock, since a version is written whole and never changes; null means the bot is unknown.
    public static async Task<List<VersionSummary>?> ListAsync(Pool pool, string bot, CancellationToken cancellationToken)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        if (!await ResourceStore.BotExistsAsync(connection, null, bot, cancellationToken))
        {
            return null;
        }
        var rows = await connection.QueryAsync<ListedVersion>(
            new CommandDefinition(
                $"SELECT number AS Number, {Counts} FROM bot_version WHERE bot_id = @bot ORDER BY number DESC",
                new { bot },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows.Select(Summary)];
    }

    public static async Task<VersionDetail?> FindAsync(Pool pool, string bot, int number, CancellationToken cancellationToken)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<StoredVersion>(
            new CommandDefinition(
                $"SELECT document AS Document, {Counts} FROM bot_version WHERE bot_id = @bot AND number = @number",
                new { bot, number },
                cancellationToken: cancellationToken
            )
        );
        return row is null
            ? null
            : new VersionDetail(
                Summary(new ListedVersion(number, row.CreatedAt, row.Facts, row.KnowledgeBases, row.Articles, row.Procedures, row.Sha256)),
                row.Document
            );
    }

    // Why: the latest version's number and digest, read under the bot's lock, are all that decides the next number and whether anything changed.
    public static async Task<Latest?> LatestAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string bot,
        CancellationToken cancellationToken
    )
    {
        return await connection.QuerySingleOrDefaultAsync<Latest>(
            new CommandDefinition(
                "SELECT number, sha256 FROM bot_version WHERE bot_id = @bot ORDER BY number DESC LIMIT 1",
                new { bot },
                transaction,
                cancellationToken: cancellationToken
            )
        );
    }

    public static async Task InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string bot,
        int number,
        string document,
        string sha256,
        DateTimeOffset now,
        CancellationToken cancellationToken
    ) =>
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO bot_version (bot_id, number, format_version, document, sha256, created_at)
                VALUES (@bot, @number, @formatVersion, @document, @sha256, @now)
                """,
                new
                {
                    bot,
                    number,
                    formatVersion = Suwagcms.Sdk.Format.BotDefinition.CurrentFormatVersion,
                    document,
                    sha256,
                    now,
                },
                transaction,
                cancellationToken: cancellationToken
            )
        );

    private static VersionSummary Summary(ListedVersion row) =>
        new(
            row.Number,
            new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc)),
            row.Facts,
            row.KnowledgeBases,
            row.Articles,
            row.Procedures,
            row.Sha256
        );
}
