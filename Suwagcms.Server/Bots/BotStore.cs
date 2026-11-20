using Dapper;
using Npgsql;
using Suwagcms.Contracts.Bots;

namespace Suwagcms.Server.Bots;

internal static class BotStore
{
    public static async Task<List<Bot>> ListAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Bot>(
            new CommandDefinition("SELECT id, name, address FROM bot ORDER BY id", cancellationToken: cancellationToken)
        );
        return [.. rows];
    }

    public static async Task<string?> FindAddressAsync(NpgsqlDataSource dataSource, string bot, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT address FROM bot WHERE id = @bot", new { bot }, cancellationToken: cancellationToken)
        );
    }

    public static async Task<bool> AddAsync(NpgsqlDataSource dataSource, Bot bot, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
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
