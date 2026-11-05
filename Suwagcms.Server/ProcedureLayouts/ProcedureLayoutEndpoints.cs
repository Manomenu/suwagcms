using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.ProcedureLayouts;

// Why: positions are the creator's, outside the procedure and its revision, so they have their own routes and never go through the editor.
internal static class ProcedureLayoutEndpoints
{
    private const string Problem = "application/problem+json";
    private const string Title = "The layout was not saved.";
    private static readonly string ProcedureColumn = ResourceKinds.Column(ResourceKind.Procedure);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static void MapProcedureLayouts(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bots/{bot}/procedures/{id}/layout", Get).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes
            .MapPut("/bots/{bot}/procedures/{id}/layout", Save)
            .Accepts<ProcedureLayout>("application/json")
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem)
            .Produces<Refusal>(StatusCodes.Status422UnprocessableEntity, Problem);
    }

    private sealed record StoredPositions(string? Positions);

    private static async Task<Results<Ok<ProcedureLayout>, ProblemHttpResult>> Get(
        string bot,
        string id,
        Pool pool,
        CancellationToken cancellationToken
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        if (!await ResourceStore.BotExistsAsync(connection, null, bot, cancellationToken))
        {
            return EditProblems.For(new EditResult.UnknownBot(), bot, "The layout was not shown.");
        }
        var rows = (
            await connection.QueryAsync<StoredPositions>(
                new CommandDefinition(
                    """
                    SELECT l.positions::text AS Positions
                    FROM resource r LEFT JOIN procedure_layout l ON l.resource_id = r.id
                    WHERE r.bot_id = @bot AND r.kind = @kind AND r.public_id = @id
                    """,
                    new
                    {
                        bot,
                        kind = ProcedureColumn,
                        id,
                    },
                    cancellationToken: cancellationToken
                )
            )
        ).ToList();
        return rows.Count == 0
            ? EditProblems.For(new EditResult.Missing(new ResourceRef(ResourceKind.Procedure, id)), bot, "The layout was not shown.")
            : TypedResults.Ok(
                new ProcedureLayout(
                    rows[0].Positions is { } text
                        ? JsonSerializer.Deserialize<Dictionary<string, NodePosition>>(text, Web) ?? []
                        : new Dictionary<string, NodePosition>()
                )
            );
    }

    private static async Task<Results<Ok<ProcedureLayout>, ProblemHttpResult>> Save(
        string bot,
        string id,
        HttpRequest request,
        Pool pool,
        CancellationToken cancellationToken
    )
    {
        var target = new ResourceRef(ResourceKind.Procedure, id);
        var (body, unreadable) = await StrictBody.ReadAsync<ProcedureLayout>(request.Body, target, cancellationToken);
        if (body is null)
        {
            return EditProblems.For(new EditResult.Refused(unreadable), bot, Title);
        }
        await using var connection = await pool.OpenAsync(cancellationToken);
        if (!await ResourceStore.BotExistsAsync(connection, null, bot, cancellationToken))
        {
            return EditProblems.For(new EditResult.UnknownBot(), bot, Title);
        }
        // Why: no positions means "arrange it for me", so the row goes instead of holding an empty map.
        var saved =
            body.Positions.Count == 0
                ? await Delete(connection, bot, id, cancellationToken)
                : await Upsert(connection, bot, id, JsonSerializer.Serialize(body.Positions, Web), cancellationToken);
        return saved ? TypedResults.Ok(body) : EditProblems.For(new EditResult.Missing(target), bot, Title);
    }

    private static async Task<bool> Upsert(
        Npgsql.NpgsqlConnection connection,
        string bot,
        string id,
        string positions,
        CancellationToken cancellationToken
    ) =>
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO procedure_layout (resource_id, positions)
                SELECT id, @positions::jsonb FROM resource WHERE bot_id = @bot AND kind = @kind AND public_id = @id
                ON CONFLICT (resource_id) DO UPDATE SET positions = EXCLUDED.positions
                """,
                new
                {
                    bot,
                    kind = ProcedureColumn,
                    id,
                    positions,
                },
                cancellationToken: cancellationToken
            )
        ) > 0;

    private static async Task<bool> Delete(
        Npgsql.NpgsqlConnection connection,
        string bot,
        string id,
        CancellationToken cancellationToken
    ) =>
        await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                WITH target AS (SELECT id FROM resource WHERE bot_id = @bot AND kind = @kind AND public_id = @id),
                     gone AS (DELETE FROM procedure_layout WHERE resource_id IN (SELECT id FROM target))
                SELECT EXISTS (SELECT 1 FROM target)
                """,
                new
                {
                    bot,
                    kind = ProcedureColumn,
                    id,
                },
                cancellationToken: cancellationToken
            )
        );
}
