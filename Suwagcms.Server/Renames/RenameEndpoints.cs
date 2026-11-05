using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;
using Suwagcms.Server.Editing;
using Suwagcms.Server.Procedures;

namespace Suwagcms.Server.Renames;

internal static class RenameEndpoints
{
    private const string Title = "The id was not changed.";
    private const string Problem = "application/problem+json";

    // Why: the route accepts only these segments, so the lookup cannot miss.
    private static readonly Dictionary<string, ResourceKind> Kinds = new(StringComparer.Ordinal)
    {
        ["facts"] = ResourceKind.GlobalFact,
        ["knowledge-bases"] = ResourceKind.KnowledgeBase,
        ["articles"] = ResourceKind.Article,
        ["procedures"] = ResourceKind.Procedure,
    };

    public static void MapRenames(this IEndpointRouteBuilder routes) =>
        routes
            .MapPost("/bots/{bot}/{kind:regex(^(facts|knowledge-bases|articles|procedures)$)}/{id}/rename", Apply)
            .Accepts<RenameResource>("application/json")
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem)
            .Produces<Refusal>(StatusCodes.Status409Conflict, Problem)
            .Produces<Refusal>(StatusCodes.Status422UnprocessableEntity, Problem);

    private static async Task<Results<Ok<RenameAnswer>, ProblemHttpResult>> Apply(
        string bot,
        string kind,
        string id,
        HttpRequest request,
        DefinitionEditor editor,
        CancellationToken cancellationToken,
        bool dryRun = false,
        int? ifRevision = null
    )
    {
        var target = new ResourceRef(Kinds[kind], id);
        var (body, unreadable) = await StrictBody.ReadAsync<RenameResource>(request.Body, target, cancellationToken);
        if (body is null)
        {
            return EditProblems.For(new EditResult.Refused(unreadable), bot, Title);
        }
        if (target == new ResourceRef(ResourceKind.Procedure, Procedure.ConversationId))
        {
            return EditProblems.For(new EditResult.Refused([ProcedureEndpoints.IdRequired(target)]), bot, Title);
        }
        var change = new Rename(target, body.NewId ?? "", ifRevision);
        var result = await editor.ApplyAsync(bot, [change], dryRun, cancellationToken);
        return result is EditResult.Saved saved
            ? TypedResults.Ok(
                new RenameAnswer(
                    ResourceKinds.Noun(target.Kind),
                    id,
                    change.NewId,
                    saved.Revisions[change.NewTarget],
                    [
                        .. saved
                            .Rewritten.GroupBy(reference => (reference.From, reference.Path))
                            .Select(field => new RenameReferrer(
                                ResourceKinds.Noun(field.Key.From.Kind),
                                field.Key.From.Id,
                                field.Key.Path,
                                field.Count(),
                                saved.Revisions[field.Key.From]
                            ))
                            .OrderBy(referrer => referrer.Kind, StringComparer.Ordinal)
                            .ThenBy(referrer => referrer.Id, StringComparer.Ordinal)
                            .ThenBy(referrer => referrer.Field, StringComparer.Ordinal),
                    ]
                )
            )
            : EditProblems.For(result, bot, Title);
    }
}
