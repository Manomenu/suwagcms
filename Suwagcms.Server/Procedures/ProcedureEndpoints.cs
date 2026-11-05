using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Procedures;

internal static class ProcedureEndpoints
{
    public const string ConversationRequired = "procedure.conversation.required";
    private const string Title = "The procedure was not saved.";
    private const string Problem = "application/problem+json";

    public static void MapProcedures(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bots/{bot}/procedures", List).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes.MapGet("/bots/{bot}/procedures/{id}", Get).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes
            .MapPut("/bots/{bot}/procedures/{id}", Save)
            .Accepts<SaveProcedure>("application/json")
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem)
            .Produces<Refusal>(StatusCodes.Status409Conflict, Problem)
            .Produces<Refusal>(StatusCodes.Status422UnprocessableEntity, Problem);
        routes
            .MapDelete("/bots/{bot}/procedures/{id}", Delete)
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem)
            .Produces<Refusal>(StatusCodes.Status409Conflict, Problem)
            .Produces<Refusal>(StatusCodes.Status422UnprocessableEntity, Problem);
    }

    private static async Task<Results<Ok<ProcedureList>, ProblemHttpResult>> List(
        string bot,
        Pool pool,
        CancellationToken cancellationToken
    ) =>
        await ResourceStore.ReadAsync(pool, bot, cancellationToken) is { } stored
            ? TypedResults.Ok(ProcedureMapping.List(stored))
            : EditProblems.For(new EditResult.UnknownBot(), bot, "The procedures were not listed.");

    private static async Task<Results<Ok<ProcedureDetail>, ProblemHttpResult>> Get(
        string bot,
        string id,
        Pool pool,
        CancellationToken cancellationToken
    )
    {
        var target = new ResourceRef(ResourceKind.Procedure, id);
        if (await ResourceStore.ReadAsync(pool, bot, cancellationToken) is not { } stored)
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

    // Why: the format requires the base procedure under this id, so a change of id is refused for good.
    public static Mistake IdRequired(ResourceRef target) =>
        new(
            ConversationRequired,
            ValidationStage.Save,
            target,
            "id",
            $"'{Procedure.ConversationId}' is the base of every conversation, and the format requires it under this id, so its id cannot change.",
            $"Change what '{Procedure.ConversationId}' does with `procedures edit`, not its id.",
            []
        );

    // Why: a version needs the base procedure, so the delete is refused for good; replace its steps with an edit instead.
    public static Mistake Required(ResourceRef target) =>
        new(
            ConversationRequired,
            ValidationStage.Save,
            target,
            "id",
            $"'{Procedure.ConversationId}' is the base of every conversation, and the format requires it, so it cannot be deleted.",
            $"Change what '{Procedure.ConversationId}' does with `procedures edit`, not whether it exists.",
            []
        );
}
