using Dapper;
using Npgsql;
using Suwagcms.Contracts;
using Suwagcms.Server.Database;

namespace Suwagcms.Server.Bots;

internal static class BotStore
{
    public static async Task<List<Bot>> ListAsync(Pool pool, CancellationToken cancellationToken)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<Bot>(
            new CommandDefinition("SELECT id, name, address FROM bot ORDER BY id", cancellationToken: cancellationToken)
        );
        return [.. rows];
    }

    // Why: the primary key decides between two registrations of one id at the same moment; false means the id was taken.
    public static async Task<bool> AddAsync(Pool pool, Bot bot, CancellationToken cancellationToken)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    "INSERT INTO bot (id, name, address) VALUES (@Id, @Name, @Address)",
                    bot,
                    cancellationToken: cancellationToken
                )
            );
            return true;
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return false;
        }
    }
}
