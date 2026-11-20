using Dapper;
using Npgsql;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Database;

namespace Suwagcms.Server.Bots;

internal static class DeclarationStore
{
    internal sealed record StoredDeclaration(string Address, string? Declaration, DateTime? DeclaredAt)
    {
        public DateTimeOffset? AnsweredAt => DeclaredAt is { } at ? new DateTimeOffset(DateTime.SpecifyKind(at, DateTimeKind.Utc)) : null;

        public BotDeclaration? Parsed => Declaration is null ? null : DeclarationValidator.ParseJson(Declaration).Declaration;
    }

    public static async Task<StoredDeclaration?> FindAsync(Pool pool, string bot, CancellationToken cancellationToken)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        return await FindAsync(connection, null, bot, cancellationToken);
    }

    public static async Task<StoredDeclaration?> FindAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string bot,
        CancellationToken cancellationToken
    ) =>
        await connection.QuerySingleOrDefaultAsync<StoredDeclaration>(
            new CommandDefinition(
                "SELECT address AS Address, declaration AS Declaration, declared_at AS DeclaredAt FROM bot WHERE id = @bot",
                new { bot },
                transaction,
                cancellationToken: cancellationToken
            )
        );

    public static async Task StoreAsync(Pool pool, string bot, string? declaration, DateTimeOffset at, CancellationToken cancellationToken)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE bot SET declaration = @declaration, declared_at = @at
                WHERE id = @bot AND (declared_at IS NULL OR declared_at <= @at)
                """,
                new
                {
                    bot,
                    declaration,
                    at,
                },
                cancellationToken: cancellationToken
            )
        );
    }
}
