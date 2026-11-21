using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using Suwagcms.Contracts.History;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Server.Editing;
using Suwagcms.Server.Settings;
using Suwagcms.Server.Versions;
using Suwbackkit.Server;

namespace Suwagcms.Server.History;

internal static class HistoryEndpoints
{
    private const string Title = "The history was not shown.";
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
            .ProducesRefusal(StatusCodes.Status404NotFound)
            .ProducesRefusal(StatusCodes.Status400BadRequest);
        routes
            .MapGet(
                "/bots/{bot}/{kind:regex(^(facts|knowledge-bases|articles|procedures|actions|persona|versions)$)}/{id}/history",
                OfResource
            )
            .ProducesRefusal(StatusCodes.Status404NotFound);
        routes.MapGet("/bots/{bot}/versions/{number:int}/window", OfWindow).ProducesRefusal(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<HistoryPage>, ProblemHttpResult>> OfBot(
        string bot,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken,
        int limit = DefaultLimit,
        long? before = null,
        int? page = null,
        string? kind = null
    )
    {
        if (kind is null)
        {
            return await PageAsync(bot, null, dataSource, limit, before, page, cancellationToken);
        }
        if (!Kinds.TryGetValue(kind, out var only))
        {
            return Refusals.Refuse(
                StatusCodes.Status400BadRequest,
                Title,
                RefusalCode.HistoryKindUnknown,
                $"There is no kind '{kind}'. Ask for one of: {string.Join(", ", Kinds.Keys)}, or leave `kind` out for every change."
            );
        }
        return await PageAsync(bot, null, dataSource, limit, before, page, cancellationToken, only);
    }

    private static Task<Results<Ok<HistoryPage>, ProblemHttpResult>> OfResource(
        string bot,
        string kind,
        string id,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken,
        int limit = DefaultLimit,
        long? before = null,
        int? page = null
    ) => PageAsync(bot, (Kinds[kind], id), dataSource, limit, before, page, cancellationToken);

    private static async Task<Results<Ok<HistoryPage>, ProblemHttpResult>> OfWindow(
        string bot,
        int number,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken,
        int limit = DefaultLimit,
        long? before = null,
        int? page = null
    )
    {
        if (!await VersionStore.ExistsAsync(dataSource, bot, number, cancellationToken))
        {
            return await VersionEndpoints.MissingAsync(dataSource, bot, number, Title, cancellationToken);
        }
        var answer = await HistoryStore.ReadWindowAsync(
            dataSource,
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
        NpgsqlDataSource dataSource,
        int limit,
        long? before,
        int? page,
        CancellationToken cancellationToken,
        string? only = null
    ) =>
        await HistoryStore.ReadAsync(dataSource, bot, resource, Math.Clamp(limit, 1, PageSize.Most), before, page, cancellationToken, only)
            is { } answer
            ? TypedResults.Ok(WrittenWhenPaged(answer, page))
            : EditProblems.For(new EditResult.UnknownBot(), bot, Title);
}
