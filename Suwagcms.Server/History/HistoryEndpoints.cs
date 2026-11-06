using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Contracts;
using Suwagcms.Contracts.History;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;
using Suwagcms.Server.Settings;

namespace Suwagcms.Server.History;

// Why: the two routes only read: the record cannot be edited, and a guard in the tests fails when a route that writes it appears.
internal static class HistoryEndpoints
{
    private const string Title = "The history was not shown.";
    private const string Problem = "application/problem+json";
    private const int DefaultLimit = 50;

    // Why: the route accepts only these segments, so the lookup cannot miss; a deleted resource is found the same way, since nothing here asks whether it still exists.
    private static readonly Dictionary<string, string> Kinds = new(StringComparer.Ordinal)
    {
        ["facts"] = ResourceKinds.Word(ResourceKind.GlobalFact),
        ["knowledge-bases"] = ResourceKinds.Word(ResourceKind.KnowledgeBase),
        ["articles"] = ResourceKinds.Word(ResourceKind.Article),
        ["procedures"] = ResourceKinds.Word(ResourceKind.Procedure),
        ["actions"] = ResourceKinds.Word(ResourceKind.Action),
        ["persona"] = ResourceKinds.Word(ResourceKind.Persona),
        ["versions"] = HistoryStore.VersionKind,
    };

    public static void MapHistory(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bots/{bot}/history", OfBot).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes
            .MapGet(
                "/bots/{bot}/{kind:regex(^(facts|knowledge-bases|articles|procedures|actions|persona|versions)$)}/{id}/history",
                OfResource
            )
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
    }

    private static Task<Results<Ok<HistoryPage>, ProblemHttpResult>> OfBot(
        string bot,
        Pool pool,
        CancellationToken cancellationToken,
        int limit = DefaultLimit,
        long? before = null,
        int? page = null
    ) => PageAsync(bot, null, pool, limit, before, page, cancellationToken);

    private static Task<Results<Ok<HistoryPage>, ProblemHttpResult>> OfResource(
        string bot,
        string kind,
        string id,
        Pool pool,
        CancellationToken cancellationToken,
        int limit = DefaultLimit,
        long? before = null,
        int? page = null
    ) => PageAsync(bot, (Kinds[kind], id), pool, limit, before, page, cancellationToken);

    private static async Task<Results<Ok<HistoryPage>, ProblemHttpResult>> PageAsync(
        string bot,
        (string Kind, string Id)? resource,
        Pool pool,
        int limit,
        long? before,
        int? page,
        CancellationToken cancellationToken
    ) =>
        await HistoryStore.ReadAsync(pool, bot, resource, Math.Clamp(limit, 1, PageSize.Most), before, page, cancellationToken)
            is { } answer
            ? TypedResults.Ok(answer)
            : EditProblems.For(new EditResult.UnknownBot(), bot, Title);
}
