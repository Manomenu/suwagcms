using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using Suwagcms.Contracts.Facts;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Facts;

internal static class FactEndpoints
{
    private const string Title = "The fact was not saved.";

    public static void MapFacts(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bots/{bot}/facts", List).ProducesRefusal(StatusCodes.Status404NotFound);
        routes.MapGet("/bots/{bot}/facts/{name}", Get).ProducesRefusal(StatusCodes.Status404NotFound);
        routes
            .MapPut("/bots/{bot}/facts/{name}", Save)
            .Accepts<SaveFact>("application/json")
            .ProducesRefusal(StatusCodes.Status404NotFound)
            .ProducesRefusal(StatusCodes.Status409Conflict)
            .ProducesRefusal(StatusCodes.Status422UnprocessableEntity);
        routes
            .MapDelete("/bots/{bot}/facts/{name}", Delete)
            .ProducesRefusal(StatusCodes.Status404NotFound)
            .ProducesRefusal(StatusCodes.Status409Conflict)
            .ProducesRefusal(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<Results<Ok<List<Fact>>, ProblemHttpResult>> List(
        string bot,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken
    ) =>
        await ResourceStore.ReadAsync(dataSource, bot, cancellationToken) is { } stored
            ? TypedResults.Ok(
                stored.Where(item => item.Target.Kind == ResourceKind.GlobalFact).Select(item => FactMapping.Show(item, stored)).ToList()
            )
            : EditProblems.For(new EditResult.UnknownBot(), bot, "The facts were not listed.");

    private static async Task<Results<Ok<Fact>, ProblemHttpResult>> Get(
        string bot,
        string name,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken
    )
    {
        var target = new ResourceRef(ResourceKind.GlobalFact, name);
        if (await ResourceStore.ReadAsync(dataSource, bot, cancellationToken) is not { } stored)
        {
            return EditProblems.For(new EditResult.UnknownBot(), bot, "The fact was not shown.");
        }
        return stored.SingleOrDefault(item => item.Target == target) is { } found
            ? TypedResults.Ok(FactMapping.Show(found, stored))
            : EditProblems.For(new EditResult.Missing(target), bot, "The fact was not shown.");
    }

    private static async Task<Results<Ok<Fact>, ProblemHttpResult>> Save(
        string bot,
        string name,
        HttpRequest request,
        DefinitionEditor editor,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken,
        bool dryRun = false,
        int? ifRevision = null
    )
    {
        var target = new ResourceRef(ResourceKind.GlobalFact, name);
        var (body, unreadable) = await StrictBody.ReadAsync<SaveFact>(request.Body, target, cancellationToken);
        if (body is null)
        {
            return EditProblems.For(new EditResult.Refused(unreadable), bot, Title);
        }
        var result = await editor.ApplyAsync(
            bot,
            [new Put(target, FactMapping.ToRecord(name, body), ifRevision)],
            dryRun,
            cancellationToken
        );
        if (result is not EditResult.Saved saved)
        {
            return EditProblems.For(result, bot, Title);
        }
        var all = await ResourceStore.ReadAsync(dataSource, bot, cancellationToken) ?? [];
        return TypedResults.Ok(FactMapping.Show(FactMapping.ToRecord(name, body), saved.Revisions[target], all));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Delete(
        string bot,
        string name,
        DefinitionEditor editor,
        CancellationToken cancellationToken,
        bool dryRun = false,
        int? ifRevision = null
    )
    {
        var target = new ResourceRef(ResourceKind.GlobalFact, name);
        var result = await editor.ApplyAsync(bot, [new Remove(target, ifRevision)], dryRun, cancellationToken);
        return result is EditResult.Saved ? TypedResults.NoContent() : EditProblems.For(result, bot, "The fact was not deleted.");
    }
}
