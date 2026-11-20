using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Contracts;
using Suwagcms.Contracts.Usage;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Usage;

internal static class UsageEndpoints
{
    private const string Title = "The usage was not shown.";
    private const string Problem = "application/problem+json";

    private static readonly Dictionary<string, ResourceKind> Kinds = new(StringComparer.Ordinal)
    {
        ["facts"] = ResourceKind.GlobalFact,
        ["articles"] = ResourceKind.Article,
        ["procedures"] = ResourceKind.Procedure,
        ["actions"] = ResourceKind.Action,
    };

    public static void MapUsage(this IEndpointRouteBuilder routes) =>
        routes
            .MapGet("/bots/{bot}/{kind:regex(^(facts|articles|procedures|actions)$)}/{id}/usage", Show)
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem);

    private static async Task<Results<Ok<UsageView>, ProblemHttpResult>> Show(
        string bot,
        string kind,
        string id,
        Pool pool,
        CancellationToken cancellationToken
    )
    {
        var target = new ResourceRef(Kinds[kind], id);
        if (await ResourceStore.ReadAsync(pool, bot, cancellationToken) is not { } stored)
        {
            return EditProblems.For(new EditResult.UnknownBot(), bot, Title);
        }
        if (stored.All(item => item.Target != target))
        {
            return EditProblems.For(new EditResult.Missing(target), bot, Title);
        }
        return TypedResults.Ok(ResourceUsage.Of(stored, target));
    }
}
