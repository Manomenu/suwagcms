using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Contracts;
using Suwagcms.Contracts.Changes;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Changes;

internal static class ChangesEndpoints
{
    private const string Title = "The changes were not applied.";
    private const string Problem = "application/problem+json";

    public static void MapChanges(this IEndpointRouteBuilder routes) =>
        routes
            .MapPost("/bots/{bot}/changes", Apply)
            .Accepts<ApplyChanges>("application/json")
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem)
            .Produces<ChangesRefusal>(StatusCodes.Status422UnprocessableEntity, Problem);

    private static async Task<Results<Ok<ChangesApplied>, ProblemHttpResult>> Apply(
        string bot,
        HttpRequest request,
        DefinitionEditor editor,
        CancellationToken cancellationToken,
        bool dryRun = false
    )
    {
        var (items, unreadable) = await ChangeList.ReadAsync(request.Body, bot, cancellationToken);
        if (unreadable.Count > 0)
        {
            return Refused(unreadable);
        }
        var result = await editor.ApplyAsync(bot, [.. items.Select(item => item.Change)], dryRun, cancellationToken);
        return result switch
        {
            EditResult.Saved saved => TypedResults.Ok(Answer(items, saved, dryRun)),
            EditResult.UnknownBot => EditProblems.For(result, bot, Title),
            _ => Refused([.. EditProblems.MistakesOf(result, bot).Select(mistake => Placed(mistake, items))]),
        };
    }

    private static ChangesApplied Answer(List<ChangeItem> items, EditResult.Saved saved, bool dryRun) =>
        new(
            items.Count(item => item.Action == "added"),
            items.Count(item => item.Action == "edited"),
            items.Count(item => item.Action == "deleted"),
            dryRun,
            [
                .. items.Select(item => new ChangeApplied(
                    item.Position,
                    item.Action,
                    item.Kind,
                    item.Change.Target.Id,
                    saved.Revisions.TryGetValue(item.Change.Target, out var revision) ? revision : null
                )),
            ],
            saved.NotReady
        );

    // Why: the rules name a resource and not a position; a mistake on a resource the file does not name has no position to give.
    private static ChangeMistake Placed(Mistake mistake, List<ChangeItem> items) =>
        ChangeList.At(items.Find(item => item.Change.Target == mistake.Resource)?.Position, mistake);

    private static ProblemHttpResult Refused(List<ChangeMistake> mistakes) =>
        TypedResults.Problem(
            title: Title,
            detail: $"{mistakes.Count} mistake(s) in `mistakes`: nothing was saved. Fix each as its `fix` says, then apply the whole file again.",
            statusCode: StatusCodes.Status422UnprocessableEntity,
            extensions: new Dictionary<string, object?>
            {
                ["mistakes"] = mistakes.OrderBy(mistake => mistake.Change ?? int.MaxValue).ToList(),
            }
        );
}
