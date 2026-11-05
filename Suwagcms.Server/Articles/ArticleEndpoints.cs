using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Articles;

internal static class ArticleEndpoints
{
    private const string Title = "The article was not saved.";
    private const string Problem = "application/problem+json";

    public static void MapArticles(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bots/{bot}/articles", List).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes.MapGet("/bots/{bot}/articles/{id}", Get).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes
            .MapPut("/bots/{bot}/articles/{id}", Save)
            .Accepts<SaveArticle>("application/json")
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem)
            .Produces<Refusal>(StatusCodes.Status409Conflict, Problem)
            .Produces<Refusal>(StatusCodes.Status422UnprocessableEntity, Problem);
        routes
            .MapDelete("/bots/{bot}/articles/{id}", Delete)
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem)
            .Produces<Refusal>(StatusCodes.Status409Conflict, Problem)
            .Produces<Refusal>(StatusCodes.Status422UnprocessableEntity, Problem);
    }

    private static async Task<Results<Ok<List<ArticleView>>, ProblemHttpResult>> List(
        string bot,
        Pool pool,
        CancellationToken cancellationToken,
        string? knowledgeBase = null,
        string? text = null,
        string? language = null
    ) =>
        await ResourceStore.ReadAsync(pool, bot, cancellationToken) is { } stored
            ? TypedResults.Ok(ListOf(stored, knowledgeBase, text, language))
            : EditProblems.For(new EditResult.UnknownBot(), bot, "The articles were not listed.");

    private static List<ArticleView> ListOf(List<StoredResource> stored, string? knowledgeBase, string? text, string? language)
    {
        var languages = ArticleMapping.Languages(stored);
        return
        [
            .. stored
                .Where(item =>
                    item.Target.Kind == ResourceKind.Article
                    && ArticleMapping.Matches((Article)item.Record, knowledgeBase, text, language, languages)
                )
                .Select(item => ArticleMapping.Show(item, languages)),
        ];
    }

    private static async Task<Results<Ok<ArticleView>, ProblemHttpResult>> Get(
        string bot,
        string id,
        Pool pool,
        CancellationToken cancellationToken
    )
    {
        var target = new ResourceRef(ResourceKind.Article, id);
        if (await ResourceStore.ReadAsync(pool, bot, cancellationToken) is not { } stored)
        {
            return EditProblems.For(new EditResult.UnknownBot(), bot, "The article was not shown.");
        }
        return stored.SingleOrDefault(item => item.Target == target) is { } found
            ? TypedResults.Ok(ArticleMapping.Show(found, ArticleMapping.Languages(stored)))
            : EditProblems.For(new EditResult.Missing(target), bot, "The article was not shown.");
    }

    private static async Task<Results<Ok<ArticleView>, ProblemHttpResult>> Save(
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
        var target = new ResourceRef(ResourceKind.Article, id);
        var (body, unreadable) = await StrictBody.ReadAsync<SaveArticle>(request.Body, target, cancellationToken);
        if (body is null)
        {
            return EditProblems.For(new EditResult.Refused(unreadable), bot, Title);
        }
        var record = ArticleMapping.ToRecord(id, body);
        var result = await editor.ApplyAsync(bot, [new Put(target, record, ifRevision)], dryRun, cancellationToken);
        return result is EditResult.Saved saved
            ? TypedResults.Ok(
                ArticleMapping.Show(
                    record,
                    saved.Revisions[target],
                    ArticleMapping.Languages(await ResourceStore.ReadAsync(pool, bot, cancellationToken) ?? [])
                )
            )
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
        var target = new ResourceRef(ResourceKind.Article, id);
        var result = await editor.ApplyAsync(bot, [new Remove(target, ifRevision)], dryRun, cancellationToken);
        return result is EditResult.Saved ? TypedResults.NoContent() : EditProblems.For(result, bot, "The article was not deleted.");
    }
}
