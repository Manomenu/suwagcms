using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using Suwagcms.Contracts.Procedures;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Procedures;

internal static class ProcedureEndpoints
{
    private const string Title = "The procedure was not saved.";

    public static void MapProcedures(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bots/{bot}/procedures", List).ProducesRefusal(StatusCodes.Status404NotFound);
        routes.MapGet("/bots/{bot}/procedures/{id}", Get).ProducesRefusal(StatusCodes.Status404NotFound);
        routes
            .MapPut("/bots/{bot}/procedures/{id}", Save)
            .Accepts<SaveProcedure>("application/json")
            .ProducesRefusal(StatusCodes.Status404NotFound)
            .ProducesRefusal(StatusCodes.Status409Conflict)
            .ProducesRefusal(StatusCodes.Status422UnprocessableEntity);
        routes
            .MapDelete("/bots/{bot}/procedures/{id}", Delete)
            .ProducesRefusal(StatusCodes.Status404NotFound)
            .ProducesRefusal(StatusCodes.Status409Conflict)
            .ProducesRefusal(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<Results<Ok<ProcedureList>, ProblemHttpResult>> List(
        string bot,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken
    ) =>
        await ResourceStore.ReadAsync(dataSource, bot, cancellationToken) is { } stored
            ? TypedResults.Ok(ProcedureMapping.List(stored))
            : EditProblems.For(new EditResult.UnknownBot(), bot, "The procedures were not listed.");

    private static async Task<Results<Ok<ProcedureDetail>, ProblemHttpResult>> Get(
        string bot,
        string id,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken
    )
    {
        var target = new ResourceRef(ResourceKind.Procedure, id);
        if (await ResourceStore.ReadAsync(dataSource, bot, cancellationToken) is not { } stored)
        {
            return EditProblems.For(new EditResult.UnknownBot(), bot, "The procedure was not shown.");
        }
        return stored.SingleOrDefault(item => item.Target == target) is { } found
            ? TypedResults.Ok(ProcedureMapping.Show(found, ProcedureMapping.NotReadyOf(stored)))
            : EditProblems.For(new EditResult.Missing(target), bot, "The procedure was not shown.");
    }

    private static async Task<Results<Ok<ProcedureDetail>, ProblemHttpResult>> Save(
        string bot,
        string id,
        HttpRequest request,
        DefinitionEditor editor,
        CancellationToken cancellationToken,
        bool dryRun = false,
        int? ifRevision = null
    )
    {
        var target = new ResourceRef(ResourceKind.Procedure, id);
        var (body, unreadable) = await StrictBody.ReadAsync<SaveProcedure>(request.Body, target, cancellationToken);
        if (body is null)
        {
            return EditProblems.For(new EditResult.Refused(unreadable), bot, Title);
        }
        var record = ProcedureMapping.ToRecord(id, body);
        var result = await editor.ApplyAsync(bot, [new Put(target, record, ifRevision)], dryRun, cancellationToken);
        return result is EditResult.Saved saved
            ? TypedResults.Ok(ProcedureMapping.Show(record, saved.Revisions[target], saved.NotReady))
            : EditProblems.For(result, bot, Title);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(
        string bot,
        string id,
        DefinitionEditor editor,
        CancellationToken cancellationToken,
        bool dryRun = false,
        int? ifRevision = null
    )
    {
        var target = new ResourceRef(ResourceKind.Procedure, id);
        if (id == Procedure.ConversationId)
        {
            return EditProblems.For(new EditResult.Refused([Required(target)]), bot, "The procedure was not deleted.");
        }
        var result = await editor.ApplyAsync(bot, [new Remove(target, ifRevision)], dryRun, cancellationToken);
        return result is EditResult.Saved ? TypedResults.NoContent() : EditProblems.For(result, bot, "The procedure was not deleted.");
    }

    public static Mistake IdRequired(ResourceRef target) =>
        new(
            MistakeCode.ProcedureConversationRequired,
            ValidationStage.Save,
            target,
            "id",
            $"'{Procedure.ConversationId}' is the base of every conversation, and the format requires it under this id, so its id cannot change.",
            $"Change what '{Procedure.ConversationId}' does with `procedures edit`, not its id.",
            []
        );

    public static Mistake Required(ResourceRef target) =>
        new(
            MistakeCode.ProcedureConversationRequired,
            ValidationStage.Save,
            target,
            "id",
            $"'{Procedure.ConversationId}' is the base of every conversation, and the format requires it, so it cannot be deleted.",
            $"Change what '{Procedure.ConversationId}' does with `procedures edit`, not whether it exists.",
            []
        );
}
