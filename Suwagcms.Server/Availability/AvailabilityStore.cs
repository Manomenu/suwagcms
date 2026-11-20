using System.Text.Json;
using Dapper;
using Npgsql;
using Suwagcms.Contracts.Availability;
using Suwagcms.Contracts.History;
using Suwagcms.Contracts.Usage;
using Suwagcms.Sdk.BotRuntime.Availability;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Availability;

internal static class AvailabilityStore
{
    internal enum Outcome
    {
        UnknownBot,
        Missing,
        Changed,
        AlreadySo,
    }

    private static readonly IReadOnlyList<UsageLink> NoOne = [];

    private sealed record OffRow(string Kind, string PublicId, int? UpToVersion);

    private sealed record ResourceRow(string Kind, string PublicId, bool Off);

    public static async Task<(Outcome Result, IReadOnlyList<UsageLink> UsedBy)> ChangeAsync(
        NpgsqlDataSource dataSource,
        string bot,
        ResourceRef target,
        bool off,
        bool dryRun,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ResourceStore.LockBotAsync(connection, transaction, bot, cancellationToken);
        if (!await ResourceStore.BotExistsAsync(connection, transaction, bot, cancellationToken))
        {
            return (Outcome.UnknownBot, []);
        }
        var resources = await ResourceStore.LoadAsync(connection, transaction, bot, cancellationToken);
        if (resources.Find(item => item.Target == target) is not { } found)
        {
            return (Outcome.Missing, []);
        }
        var usedBy = off ? ResourceUsage.Of(resources, target).UsedBy : NoOne;
        var wasOff = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM availability WHERE resource_id = @Id)",
                new { found.Id },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        if (wasOff == off)
        {
            return (Outcome.AlreadySo, usedBy);
        }
        if (dryRun)
        {
            return (Outcome.Changed, usedBy);
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                off ? "INSERT INTO availability (resource_id) VALUES (@Id)" : "DELETE FROM availability WHERE resource_id = @Id",
                new { found.Id },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE bot SET availability_number = availability_number + 1 WHERE id = @bot",
                new { bot },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        await HistoryStore.WriteAsync(
            connection,
            transaction,
            bot,
            new HistoryStore.Recorded(
                ResourceKinds.Word(target.Kind),
                target.Id,
                off ? "turned off" : "turned on",
                null,
                [
                    new FieldChange(
                        "availability",
                        JsonSerializer.SerializeToElement(off ? "on" : "off"),
                        JsonSerializer.SerializeToElement(off ? "off" : "on")
                    ),
                ],
                Resource: found.Id
            ),
            now,
            cancellationToken
        );
        await transaction.CommitAsync(cancellationToken);
        return (Outcome.Changed, usedBy);
    }

    public static async Task<BotAvailability> ReadMessageAsync(NpgsqlDataSource dataSource, string bot, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ResourceStore.LockBotAsync(connection, transaction, bot, cancellationToken);
        var number = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT availability_number FROM bot WHERE id = @bot",
                new { bot },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        var rows = await connection.QueryAsync<OffRow>(
            new CommandDefinition(
                """
                SELECT Kind, PublicId, UpToVersion FROM (
                    SELECT r.kind AS Kind, r.public_id AS PublicId, NULL::int AS UpToVersion
                    FROM availability a JOIN resource r ON r.id = a.resource_id
                    WHERE r.bot_id = @bot
                    UNION ALL
                    SELECT r.kind, f.public_id, f.up_to_version
                    FROM availability a
                    JOIN resource r ON r.id = a.resource_id
                    JOIN resource_former_id f ON f.resource_id = a.resource_id
                    WHERE r.bot_id = @bot AND f.public_id <> r.public_id
                ) AS off_row
                ORDER BY Kind COLLATE "C", PublicId COLLATE "C"
                """,
                new { bot },
                transaction,
                cancellationToken: cancellationToken
            )
        );
        await transaction.CommitAsync(cancellationToken);
        return new BotAvailability
        {
            Number = number,
            Off = [.. rows.Select(row => new OffResource(ResourceKinds.FromColumn(row.Kind), row.PublicId, row.UpToVersion))],
        };
    }

    public static async Task<int> ReadNumberAsync(NpgsqlDataSource dataSource, string bot, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT availability_number FROM bot WHERE id = @bot", new { bot }, cancellationToken: cancellationToken)
        );
    }

    public static async Task<List<AvailabilityItem>> ListAsync(
        NpgsqlDataSource dataSource,
        string bot,
        IReadOnlyCollection<ResourceKind> kinds,
        CancellationToken cancellationToken
    )
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ResourceRow>(
            new CommandDefinition(
                """
                SELECT r.kind AS Kind, r.public_id AS PublicId, a.resource_id IS NOT NULL AS Off
                FROM resource r LEFT JOIN availability a ON a.resource_id = r.id
                WHERE r.bot_id = @bot AND r.kind = ANY(@kinds)
                ORDER BY r.kind COLLATE "C", r.public_id COLLATE "C"
                """,
                new { bot, kinds = kinds.Select(ResourceKinds.Column).ToArray() },
                cancellationToken: cancellationToken
            )
        );
        return [.. rows.Select(row => new AvailabilityItem(ResourceKinds.Word(ResourceKinds.FromColumn(row.Kind)), row.PublicId, row.Off))];
    }
}
