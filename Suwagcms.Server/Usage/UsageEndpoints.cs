using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using Suwagcms.Contracts.Usage;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Usage;

internal static class UsageEndpoints
{
    private const string Title = "The usage was not shown.";

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
            .ProducesRefusal(StatusCodes.Status404NotFound);

    private static async Task<Results<Ok<UsageView>, ProblemHttpResult>> Show(
        string bot,
        string kind,
        string id,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken
    )
    {
        var target = new ResourceRef(Kinds[kind], id);
        if (await ResourceStore.ReadAsync(dataSource, bot, cancellationToken) is not { } stored)
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
