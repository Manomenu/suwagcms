using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Contracts.Changes;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Editing;
using Suwagcms.Server.Versions;
using Suwbackkit.Server;

namespace Suwagcms.Server.Changes;

internal static class ChangesEndpoints
{
    private const string Title = "The changes were not applied.";
    private const string Added = "added";
    private const string Replaced = "replaced";
    private const string Unchanged = "unchanged";
    private const string Edited = "edited";
    private const string Deleted = "deleted";

    public static void MapChanges(this IEndpointRouteBuilder routes) =>
        routes
            .MapPost("/bots/{bot}/changes", Apply)
            .Accepts<ApplyChanges>("application/json")
            .ProducesRefusal(StatusCodes.Status404NotFound)
            .ProducesRefusal<RefusalCode, MistakeCode, ChangeMistake>(StatusCodes.Status422UnprocessableEntity);

    private static async Task<Results<Ok<ChangesApplied>, ProblemHttpResult>> Apply(
        string bot,
        HttpRequest request,
        DefinitionEditor editor,
        CancellationToken cancellationToken,
        bool dryRun = false,
        bool replace = false
    )
    {
        var (items, unreadable) = await ChangeList.ReadAsync(request.Body, bot, replace, cancellationToken);
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

    private static ChangesApplied Answer(List<ChangeItem> items, EditResult.Saved saved, bool dryRun)
    {
        var changes = items.Select(item => Applied(item, saved)).ToList();
        var named = items.Select(item => item.Change.Target).ToHashSet();
        var kinds = named.Select(target => target.Kind).ToHashSet();
        return new(
            changes.Count(change => change.Action == Added),
            changes.Count(change => change.Action == Replaced),
            changes.Count(change => change.Action == Unchanged),
            changes.Count(change => change.Action == Edited),
            changes.Count(change => change.Action == Deleted),
            dryRun,
            changes,
            [
                .. saved
                    .Before.Keys.Where(target => kinds.Contains(target.Kind) && !named.Contains(target))
                    .Select(target => new UnmentionedResource(ResourceKinds.Word(target.Kind), target.Id))
                    .OrderBy(resource => resource.Kind, StringComparer.Ordinal)
                    .ThenBy(resource => resource.Id, StringComparer.Ordinal),
            ],
            saved.NotReady
        );
    }

    private static ChangeApplied Applied(ChangeItem item, EditResult.Saved saved)
    {
        var target = item.Change.Target;
        var was = saved.Before.GetValueOrDefault(target);
        var now = saved.After.GetValueOrDefault(target);
        var action = item.Change switch
        {
            Replace when ReferenceEquals(was, now) => Unchanged,
            Replace when was is not null && item.Action == Added => Replaced,
            _ => item.Action,
        };
        var difference = VersionComparer.Between(
            target.Kind,
            target.Id,
            was is null ? null : ResourceKinds.Write(was.Record),
            now is null ? null : ResourceKinds.Write(now.Record)
        );
        var changed = difference is { Difference: Difference.Changed } ? difference : null;
        return new ChangeApplied(item.Position, action, item.Kind, target.Id, now?.Revision, changed?.Fields ?? [], changed?.Parts ?? []);
    }

    private static ChangeMistake Placed(Mistake mistake, List<ChangeItem> items) =>
        ChangeList.At(items.Find(item => item.Change.Target == mistake.Resource)?.Position, mistake);

    private static ProblemHttpResult Refused(List<ChangeMistake> mistakes) =>
        Refusals.Refuse(
            StatusCodes.Status422UnprocessableEntity,
            Title,
            RefusalCode.ChangesInvalid,
            mistakes.OrderBy(mistake => mistake.Change ?? int.MaxValue).ToList()
        );
}
