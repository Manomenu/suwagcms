using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Usage;

internal static class UsageEndpoints
{
    private const string Title = "The usage was not shown.";
    private const string Problem = "application/problem+json";

    // Why: the route accepts only these segments, so the lookup cannot miss.
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
        // Why: the lists are computed from the definition by the walker, the same one the delete guard uses, so the panels and a refusal cannot disagree.
        // Why: a base lists its own articles on its screen, so that edge is no entry in these panels.
        var references = ReferenceWalker
            .From(ResourceKinds.Assemble(stored))
            .Where(reference => reference.To.Kind != ResourceKind.KnowledgeBase)
            .ToList();
        return TypedResults.Ok(
            new UsageView(
                Links(references.Where(reference => reference.To == target), reference => reference.From),
                Links(references.Where(reference => reference.From == target), reference => reference.To)
            )
        );
    }

    private static List<UsageLink> Links(IEnumerable<Reference> references, Func<Reference, ResourceRef> other) =>
        [
            .. references
                .Select(reference => new UsageLink(ResourceKinds.Noun(other(reference).Kind), other(reference).Id, reference.Path))
                .Distinct()
                .OrderBy(link => link.Kind, StringComparer.Ordinal)
                .ThenBy(link => link.Id, StringComparer.Ordinal)
                .ThenBy(link => link.Field, StringComparer.Ordinal),
        ];
}
