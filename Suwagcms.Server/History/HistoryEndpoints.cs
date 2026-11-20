using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Contracts;
using Suwagcms.Contracts.History;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;
using Suwagcms.Server.Settings;
using Suwagcms.Server.Versions;

namespace Suwagcms.Server.History;

internal static class HistoryEndpoints
{
    private const string Title = "The history was not shown.";
    private const string Problem = "application/problem+json";
    private const int DefaultLimit = 50;

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
        routes
            .MapGet("/bots/{bot}/history", OfBot)
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem)
            .Produces<Refusal>(StatusCodes.Status400BadRequest, Problem);
        routes
            .MapGet(
                "/bots/{bot}/{kind:regex(^(facts|knowledge-bases|articles|procedures|actions|persona|versions)$)}/{id}/history",
                OfResource
            )
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes.MapGet("/bots/{bot}/versions/{number:int}/window", OfWindow).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
    }

    private static async Task<Results<Ok<HistoryPage>, ProblemHttpResult>> OfBot(
        string bot,
        Pool pool,
        CancellationToken cancellationToken,
        int limit = DefaultLimit,
        long? before = null,
        int? page = null,
        string? kind = null
    )
    {
        if (kind is null)
        {
            return await PageAsync(bot, null, pool, limit, before, page, cancellationToken);
        }
        if (!Kinds.TryGetValue(kind, out var only))
        {
            return TypedResults.Problem(
                title: Title,
                detail: $"There is no kind '{kind}'. Ask for one of: {string.Join(", ", Kinds.Keys)}, or leave `kind` out for every change.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }
        return await PageAsync(bot, null, pool, limit, before, page, cancellationToken, only);
    }

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

    private static async Task<Results<Ok<HistoryPage>, ProblemHttpResult>> OfWindow(
        string bot,
        int number,
        Pool pool,
        CancellationToken cancellationToken,
        int limit = DefaultLimit,
        long? before = null,
        int? page = null
    )
    {
        if (!await VersionStore.ExistsAsync(pool, bot, number, cancellationToken))
        {
            return await VersionEndpoints.MissingAsync(pool, bot, number, Title, cancellationToken);
        }
        var answer = await HistoryStore.ReadWindowAsync(
            pool,
            bot,
            number,
            Math.Clamp(limit, 1, PageSize.Most),
            before,
            page,
            cancellationToken
        );
        return TypedResults.Ok(WrittenWhenPaged(answer, page));
    }

    private static HistoryPage WrittenWhenPaged(HistoryPage answer, int? page) =>
        page is null
            ? answer
            : answer with
            {
                Entries = [.. answer.Entries.Select(entry => entry with { Written = VersionComparer.Written(entry.Kind, entry.Changes) })],
            };

    private static async Task<Results<Ok<HistoryPage>, ProblemHttpResult>> PageAsync(
        string bot,
        (string Kind, string Id)? resource,
        Pool pool,
        int limit,
        long? before,
        int? page,
        CancellationToken cancellationToken,
        string? only = null
    ) =>
        await HistoryStore.ReadAsync(pool, bot, resource, Math.Clamp(limit, 1, PageSize.Most), before, page, cancellationToken, only)
            is { } answer
            ? TypedResults.Ok(WrittenWhenPaged(answer, page))
            : EditProblems.For(new EditResult.UnknownBot(), bot, Title);
}
