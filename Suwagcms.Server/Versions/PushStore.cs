using System.Globalization;
using System.Text.Json;
using Dapper;
using Suwagcms.Contracts.History;
using Suwagcms.Contracts.Versions;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Versions;

internal static class PushStore
{
    internal sealed record Registered(string Address, int? ReportedVersion, DateTimeOffset? ReportedAt);

    private sealed record BotRow(string Address, int? ReportedVersion, DateTime? ReportedAt);

    private sealed record AttemptRow(Guid Id, int VersionNumber, string Status, string? Error, DateTime At);

    public static async Task<Registered?> FindBotAsync(Pool pool, string bot, CancellationToken cancellationToken)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<BotRow>(
            new CommandDefinition(
                "SELECT address AS Address, reported_version AS ReportedVersion, reported_at AS ReportedAt FROM bot WHERE id = @bot",
                new { bot },
                cancellationToken: cancellationToken
            )
        );
        return row is null
            ? null
            : new Registered(
                row.Address,
                row.ReportedVersion,
                row.ReportedAt is { } at ? new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Utc)) : null
            );
    }

    public static async Task ReportAsync(Pool pool, string bot, int? version, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE bot SET reported_version = @version, reported_at = @at
                WHERE id = @bot AND (reported_at IS NULL OR reported_at <= @at)
                """,
                new
                {
                    bot,
                    version,
                    at,
                },
                cancellationToken: cancellationToken
            )
        );
    }

    public static async Task RecordAsync(
        Pool pool,
        string bot,
        int version,
        int? left,
        PushOutcome outcome,
        string? error,
        DateTimeOffset at,
        CancellationToken cancellationToken
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO push_attempt (id, bot_id, version_number, status, error, at)
                VALUES (@id, @bot, @version, @status, @error, @at)
                """,
                new
                {
                    id = Guid.NewGuid(),
                    bot,
                    version,
                    status = outcome.ToString(),
                    error,
                    at,
                },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        await HistoryStore.WriteAsync(
            connection,
            transaction,
            bot,
            new HistoryStore.Recorded(
                HistoryStore.VersionKind,
                version.ToString(CultureInfo.InvariantCulture),
                "pushed",
                null,
                [
                    new FieldChange(
                        "result",
                        null,
                        JsonSerializer.SerializeToElement(error is null ? outcome.ToString() : $"{outcome}: {error}")
                    ),
                    .. outcome == PushOutcome.Succeeded && left != version
                        ?
                        [
                            new FieldChange(
                                "botVersion",
                                left is { } before ? JsonSerializer.SerializeToElement(before) : null,
                                JsonSerializer.SerializeToElement(version)
                            ),
                        ]
                        : Array.Empty<FieldChange>(),
                ]
            ),
            at,
            cancellationToken
        );
        await transaction.CommitAsync(cancellationToken);
    }

    public static async Task<List<PushAttempt>> RecentAsync(Pool pool, string bot, int take, CancellationToken cancellationToken)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<AttemptRow>(
            new CommandDefinition(
                """
                SELECT id AS Id, version_number AS VersionNumber, status AS Status, error AS Error, at AS At
                FROM push_attempt WHERE bot_id = @bot ORDER BY at DESC, id LIMIT @take
                """,
                new { bot, take },
                cancellationToken: cancellationToken
            )
        );
        return
        [
            .. rows.Select(row => new PushAttempt(
                row.Id,
                row.VersionNumber,
                Enum.Parse<PushOutcome>(row.Status),
                row.Error,
                new DateTimeOffset(DateTime.SpecifyKind(row.At, DateTimeKind.Utc))
            )),
        ];
    }
}
