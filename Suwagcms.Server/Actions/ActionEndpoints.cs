using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;
using Suwagcms.Server.Bots;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Actions;

// Why: the CMS only holds the definition: nothing here calls an action's URL, because the bot runs actions and only it has the secrets.
internal static class ActionEndpoints
{
    private const string Title = "The action was not saved.";
    private const string Problem = "application/problem+json";

    public static void MapActions(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bots/{bot}/actions", List).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes.MapGet("/bots/{bot}/actions/{id}", Get).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes
            .MapPut("/bots/{bot}/actions/{id}", Save)
            .Accepts<SaveAction>("application/json")
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem)
            .Produces<Refusal>(StatusCodes.Status409Conflict, Problem)
            .Produces<Refusal>(StatusCodes.Status422UnprocessableEntity, Problem);
        routes
            .MapDelete("/bots/{bot}/actions/{id}", Delete)
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem)
            .Produces<Refusal>(StatusCodes.Status409Conflict, Problem)
            .Produces<Refusal>(StatusCodes.Status422UnprocessableEntity, Problem);
        routes
            .MapPost("/bots/{bot}/actions/{id}/test", Test)
            .Accepts<RunAction>("application/json")
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem)
            .Produces<Refusal>(StatusCodes.Status422UnprocessableEntity, Problem);
    }

    private static async Task<Results<Ok<ActionTestAnswer>, ProblemHttpResult>> Test(
        string bot,
        string id,
        HttpRequest request,
        ActionTester tester,
        CancellationToken cancellationToken
    )
    {
        const string NotRun = "The action was not run.";
        var (body, unreadable) = await StrictBody.ReadAsync<RunAction>(
            request.Body,
            new ResourceRef(ResourceKind.Action, id),
            cancellationToken
        );
        if (body is null)
        {
            return EditProblems.For(new EditResult.Refused(unreadable), bot, NotRun);
        }
        var (answer, refusal) = await tester.RunAsync(bot, id, body.Values ?? new Dictionary<string, string>(), cancellationToken);
        return answer is null
            ? EditProblems.For(refusal ?? throw new InvalidOperationException("a run gives an answer or a refusal"), bot, NotRun)
            : TypedResults.Ok(answer);
    }

    private static async Task<Results<Ok<List<ActionView>>, ProblemHttpResult>> List(
        string bot,
        Pool pool,
        CancellationToken cancellationToken
    ) =>
        await ResourceStore.ReadAsync(pool, bot, cancellationToken) is { } stored
            ? TypedResults.Ok(await ShownAsync(stored, bot, pool, cancellationToken))
            : EditProblems.For(new EditResult.UnknownBot(), bot, "The actions were not listed.");

    private static async Task<List<ActionView>> ShownAsync(
        List<StoredResource> stored,
        string bot,
        Pool pool,
        CancellationToken cancellationToken
    )
    {
        var declaration = (await DeclarationStore.FindAsync(pool, bot, cancellationToken))?.Parsed;
        return
        [
            .. stored.Where(item => item.Target.Kind == ResourceKind.Action).Select(item => ActionMapping.Show(item, stored, declaration)),
        ];
    }

    private static async Task<Results<Ok<ActionView>, ProblemHttpResult>> Get(
        string bot,
        string id,
        Pool pool,
        CancellationToken cancellationToken
    )
    {
        var target = new ResourceRef(ResourceKind.Action, id);
        if (await ResourceStore.ReadAsync(pool, bot, cancellationToken) is not { } stored)
        {
            return EditProblems.For(new EditResult.UnknownBot(), bot, "The action was not shown.");
        }
        return stored.SingleOrDefault(item => item.Target == target) is { } found
            ? TypedResults.Ok(ActionMapping.Show(found, stored, (await DeclarationStore.FindAsync(pool, bot, cancellationToken))?.Parsed))
            : EditProblems.For(new EditResult.Missing(target), bot, "The action was not shown.");
    }

    private static async Task<Results<Ok<ActionView>, ProblemHttpResult>> Save(
        string bot,
        string id,
        HttpRequest request,
        DefinitionEditor editor,
        Pool pool,
        CancellationToken cancellationToken,
        bool dryRun = false,
        int? ifRevision = null
    )
    {
        var target = new ResourceRef(ResourceKind.Action, id);
        var (body, unreadable) = await StrictBody.ReadAsync<SaveAction>(request.Body, target, cancellationToken);
        if (body is null)
        {
            return EditProblems.For(new EditResult.Refused(unreadable), bot, Title);
        }
        var record = ActionMapping.ToRecord(id, body);
        var result = await editor.ApplyAsync(bot, [new Put(target, record, ifRevision)], dryRun, cancellationToken);
        if (result is not EditResult.Saved saved)
        {
            return EditProblems.For(result, bot, Title);
        }
        // Why: a save leaves the procedures untouched, so who uses the action is read after it, outside the lock.
        var all = await ResourceStore.ReadAsync(pool, bot, cancellationToken) ?? [];
        var declaration = (await DeclarationStore.FindAsync(pool, bot, cancellationToken))?.Parsed;
        return TypedResults.Ok(ActionMapping.Show(record, saved.Revisions[target], all, declaration));
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
        var target = new ResourceRef(ResourceKind.Action, id);
        var result = await editor.ApplyAsync(bot, [new Remove(target, ifRevision)], dryRun, cancellationToken);
        return result is EditResult.Saved ? TypedResults.NoContent() : EditProblems.For(result, bot, "The action was not deleted.");
    }
}
