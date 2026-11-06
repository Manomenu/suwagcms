using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Contracts;
using Suwagcms.Contracts.KnowledgeBases;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.KnowledgeBases;

internal static class KnowledgeBaseEndpoints
{
    private const string Title = "The knowledge base was not saved.";
    private const string Problem = "application/problem+json";

    public static void MapKnowledgeBases(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bots/{bot}/knowledge-bases", List).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes.MapGet("/bots/{bot}/knowledge-bases/{id}", Get).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes
            .MapPut("/bots/{bot}/knowledge-bases/{id}", Save)
            .Accepts<SaveKnowledgeBase>("application/json")
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem)
            .Produces<Refusal>(StatusCodes.Status409Conflict, Problem)
            .Produces<Refusal>(StatusCodes.Status422UnprocessableEntity, Problem);
        routes
            .MapDelete("/bots/{bot}/knowledge-bases/{id}", Delete)
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem)
            .Produces<Refusal>(StatusCodes.Status409Conflict, Problem)
            .Produces<Refusal>(StatusCodes.Status422UnprocessableEntity, Problem);
    }

    private static async Task<Results<Ok<List<KnowledgeBaseView>>, ProblemHttpResult>> List(
        string bot,
        Pool pool,
        CancellationToken cancellationToken
    ) =>
        await ResourceStore.ReadAsync(pool, bot, cancellationToken) is { } stored
            ? TypedResults.Ok(
                stored.Where(item => item.Target.Kind == ResourceKind.KnowledgeBase).Select(KnowledgeBaseMapping.Show).ToList()
            )
            : EditProblems.For(new EditResult.UnknownBot(), bot, "The knowledge bases were not listed.");

    private static async Task<Results<Ok<KnowledgeBaseView>, ProblemHttpResult>> Get(
        string bot,
        string id,
        Pool pool,
        CancellationToken cancellationToken
    )
    {
        var target = new ResourceRef(ResourceKind.KnowledgeBase, id);
        if (await ResourceStore.ReadAsync(pool, bot, cancellationToken) is not { } stored)
        {
            return EditProblems.For(new EditResult.UnknownBot(), bot, "The knowledge base was not shown.");
        }
        return stored.SingleOrDefault(item => item.Target == target) is { } found
            ? TypedResults.Ok(KnowledgeBaseMapping.Show(found))
            : EditProblems.For(new EditResult.Missing(target), bot, "The knowledge base was not shown.");
    }

    private static async Task<Results<Ok<KnowledgeBaseView>, ProblemHttpResult>> Save(
        string bot,
        string id,
        HttpRequest request,
        DefinitionEditor editor,
        CancellationToken cancellationToken,
        bool dryRun = false,
        int? ifRevision = null
    )
    {
        var target = new ResourceRef(ResourceKind.KnowledgeBase, id);
        var (body, unreadable) = await StrictBody.ReadAsync<SaveKnowledgeBase>(request.Body, target, cancellationToken);
        if (body is null)
        {
            return EditProblems.For(new EditResult.Refused(unreadable), bot, Title);
        }
        var record = KnowledgeBaseMapping.ToRecord(id, body);
        var result = await editor.ApplyAsync(bot, [new Put(target, record, ifRevision)], dryRun, cancellationToken);
        return result is EditResult.Saved saved
            ? TypedResults.Ok(KnowledgeBaseMapping.Show(record, saved.Revisions[target]))
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
        var target = new ResourceRef(ResourceKind.KnowledgeBase, id);
        var result = await editor.ApplyAsync(bot, [new Remove(target, ifRevision)], dryRun, cancellationToken);
        return result is EditResult.Saved ? TypedResults.NoContent() : EditProblems.For(result, bot, "The knowledge base was not deleted.");
    }
}
