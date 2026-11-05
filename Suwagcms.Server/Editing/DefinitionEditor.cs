using Npgsql;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Database;

namespace Suwagcms.Server.Editing;

// Why: every write of every resource goes through here, so the lock, the revisions, the whole-definition check and the rollback exist once.
internal sealed class DefinitionEditor(Pool pool, TimeProvider time)
{
    public async Task<EditResult> ApplyAsync(string bot, IReadOnlyList<Change> changes, bool dryRun, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        await using var connection = await pool.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ResourceStore.LockBotAsync(connection, transaction, bot, cancellationToken);
        if (!await ResourceStore.BotExistsAsync(connection, transaction, bot, cancellationToken))
        {
            return new EditResult.UnknownBot();
        }
        var before = (await ResourceStore.LoadAsync(connection, transaction, bot, cancellationToken)).ToDictionary(item => item.Target);

        // Why: an add of a code action's id, whatever revision it expects, is told what the id belongs to rather than only that it is taken.
        var readOnly = changes.Where(change => before.GetValueOrDefault(change.Target)?.Record is BotAction { IsCode: true }).ToList();
        if (readOnly.Count > 0)
        {
            return new EditResult.Refused([.. readOnly.Select(change => EditProblems.ReadOnly(change.Target))]);
        }

        var twice = Twice(changes);
        var unmet = Unmet(changes, before, twice);
        // Why: a list based on an old read has no result worth judging, so what is out of date comes before the rules, every piece of it at once; one piece alone keeps its own answer, a conflict or a 404.
        if (twice.Count == 0 && unmet.Count == 1)
        {
            return unmet[0];
        }
        if (twice.Count + unmet.Count > 0)
        {
            return new EditResult.Refused([
                .. twice.Select(entry => EditProblems.Twice(entry.Key, entry.Value)),
                .. unmet.SelectMany(result => EditProblems.MistakesOf(result, bot)),
            ]);
        }
        if (Conflict(changes, before) is { } conflict)
        {
            return conflict;
        }
        var rewritten = Rewrites(changes, before);
        var after = Applied(changes, before, rewritten);
        if (await CodeGuardAsync(connection, transaction, bot, changes, before, after, cancellationToken) is { } codeRefused)
        {
            return codeRefused;
        }
        // Why: one pass at the Version stage finds both kinds, since it runs every Save rule too; only the Save ones refuse. The order is fixed so that a list means the same in any order.
        var assembled = ResourceKinds.Assemble(
            after
                .Values.OrderBy(item => ResourceKinds.Column(item.Target.Kind), StringComparer.Ordinal)
                .ThenBy(item => item.Target.Id, StringComparer.Ordinal)
        );
        var found = DefinitionValidator.Validate(assembled, ValidationStage.Version);
        // Why: an edit is judged by the rules on its result and only worded for its cause, so the same break is refused however it came about.
        var edited = EditGuard.Reshape(
            ResourceKinds.Assemble(before.Values),
            assembled,
            [.. WithoutEchoes([.. found.Where(mistake => mistake.Stage == ValidationStage.Save)], changes, rewritten)]
        );
        if (edited.Count > 0)
        {
            // Why: a delete that leaves references behind is refused by the same rule as a typo, worded for the resource being deleted.
            var removed = changes.OfType<Remove>().Select(change => change.Target).ToList();
            return new EditResult.Refused(DeleteGuard.Reshape(assembled, removed, edited));
        }

        var revisions = await WriteAsync(connection, transaction, bot, before, after, dryRun, cancellationToken);
        if (dryRun)
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        else
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return new EditResult.Saved(revisions, !dryRun, found) { Rewritten = rewritten };
    }

    // Why: the bot has already changed and a refusal would only hide that, so this step judges nothing; it holds the bot's lock, so it runs in turn with any save, and reads the stored declaration under it, so two answers arriving out of order cannot leave the older one in force.
    public async Task AlignCodeActionsAsync(string bot, CancellationToken cancellationToken)
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ResourceStore.LockBotAsync(connection, transaction, bot, cancellationToken);
        if (await ResourceStore.DeclaredAsync(connection, transaction, bot, cancellationToken) is not { } declaration)
        {
            return;
        }
        var before = (await ResourceStore.LoadAsync(connection, transaction, bot, cancellationToken)).ToDictionary(item => item.Target);
        await WriteAsync(connection, transaction, bot, before, CodeActionAlignment.Apply(before, declaration), false, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<Dictionary<ResourceRef, int>> WriteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string bot,
        Dictionary<ResourceRef, StoredResource> before,
        Dictionary<ResourceRef, StoredResource> after,
        bool dryRun,
        CancellationToken cancellationToken
    )
    {
        var revisions = new Dictionary<ResourceRef, int>();
        var now = time.GetUtcNow();
        // Why: a row is known by its Id, so a renamed resource stays the same row, with what hangs on that row, and only its public id changes.
        var known = before.Values.Select(item => item.Id).ToHashSet();
        var kept = after.Values.Select(item => item.Id).ToHashSet();
        foreach (var gone in before.Values.Where(item => !kept.Contains(item.Id)).Where(_ => !dryRun))
        {
            await ResourceStore.DeleteAsync(connection, transaction, gone.Id, cancellationToken);
        }
        foreach (var becomes in after.Values.Where(item => !ReferenceEquals(item, before.GetValueOrDefault(item.Target))))
        {
            revisions[becomes.Target] = becomes.Revision;
            if (dryRun)
            {
                continue;
            }
            if (known.Contains(becomes.Id))
            {
                await ResourceStore.UpdateAsync(connection, transaction, becomes, now, cancellationToken);
            }
            else
            {
                await ResourceStore.InsertAsync(connection, transaction, bot, becomes, now, cancellationToken);
            }
        }
        if (!dryRun)
        {
            // Why: a rename also rewrites the resources that refer to it, so history covers every row that changed, not only the targets named.
            var touched = before
                .Keys.Where(target => !after.ContainsKey(target))
                .Concat(
                    after.Values.Where(item => !ReferenceEquals(item, before.GetValueOrDefault(item.Target))).Select(item => item.Target)
                )
                .ToList();
            foreach (var entry in ChangeEntries.Of(touched, before, after))
            {
                await HistoryStore.WriteAsync(connection, transaction, bot, entry, now, cancellationToken);
            }
        }
        return revisions;
    }

    // Why: what the bot's code controls has one source, so no save may touch it, whichever door the save came through; the declaration is read only when a list changes an action.
    private static async Task<EditResult.Refused?> CodeGuardAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string bot,
        IReadOnlyList<Change> changes,
        Dictionary<ResourceRef, StoredResource> before,
        Dictionary<ResourceRef, StoredResource> after,
        CancellationToken cancellationToken
    )
    {
        // Why: a stored code action never reaches here, so what is left is a save that would write one, or take an id the bot declares.
        var ofActions = changes.Where(change => change.Target.Kind == ResourceKind.Action).ToList();
        if (ofActions.Count == 0)
        {
            return null;
        }
        var declared = await ResourceStore.DeclaredAsync(connection, transaction, bot, cancellationToken);
        var mistakes = new List<Mistake>();
        foreach (var change in ofActions)
        {
            var result = change is Rename rename ? rename.NewTarget : change.Target;
            var isNew = !before.ContainsKey(result);
            if (after.GetValueOrDefault(result)?.Record is BotAction { IsCode: true })
            {
                mistakes.Add(EditProblems.CodeWritten(change.Target));
            }
            else if (isNew && after.ContainsKey(result) && declared?.CodeActions.Any(action => action.Id == result.Id) == true)
            {
                mistakes.Add(EditProblems.DeclaredId(change.Target, result));
            }
        }
        return mistakes.Count == 0 ? null : new EditResult.Refused(mistakes);
    }

    // Why: the outcome of two changes to one resource would depend on their order, and a list means the same in any order; positions count from 1, as a file is read.
    private static Dictionary<ResourceRef, List<int>> Twice(IReadOnlyList<Change> changes) =>
        changes
            .Select((change, index) => (change.Target, Position: index + 1))
            .GroupBy(item => item.Target)
            .Where(group => group.Count() > 1)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Position).ToList());

    // Why: expected revisions are checked against the state found under the lock, before any change is applied, so a stale edit loses to no mistake. A resource named twice is refused for that alone: judged against the state before the list, a delete of what the list adds would only say "missing".
    private static List<EditResult> Unmet(
        IReadOnlyList<Change> changes,
        Dictionary<ResourceRef, StoredResource> before,
        Dictionary<ResourceRef, List<int>> twice
    )
    {
        var unmet = new List<EditResult>();
        foreach (var change in changes.Where(change => !twice.ContainsKey(change.Target)))
        {
            before.TryGetValue(change.Target, out var found);
            if (change is Remove or Rename or Edit && found is null)
            {
                unmet.Add(new EditResult.Missing(change.Target));
            }
            else if (change.ExpectedRevision is { } expected && expected != (found?.Revision ?? 0))
            {
                unmet.Add(new EditResult.Stale(change.Target, expected, found?.Revision));
            }
        }
        return unmet;
    }

    // Why: a new id in the wrong form makes every link rewritten to it malformed too; that has one cause, so it is said once.
    private static IEnumerable<Mistake> WithoutEchoes(List<Mistake> mistakes, IReadOnlyList<Change> changes, List<Reference> rewritten)
    {
        var renamed = changes.OfType<Rename>().Select(rename => rename.NewTarget).ToList();
        return mistakes.Any(mistake => renamed.Contains(mistake.Resource))
            ? mistakes.Where(mistake =>
                mistake.Code != MistakeCodes.ReferenceMalformed
                || !rewritten.Any(reference => reference.From == mistake.Resource && reference.Path == mistake.Path)
            )
            : mistakes;
    }

    private static EditResult.Refused? Conflict(IReadOnlyList<Change> changes, Dictionary<ResourceRef, StoredResource> before)
    {
        foreach (var rename in changes.OfType<Rename>())
        {
            if (rename.NewId == rename.Target.Id)
            {
                return new EditResult.Refused([EditProblems.SameId(rename.Target)]);
            }
            if (before.ContainsKey(rename.NewTarget))
            {
                return new EditResult.Refused([EditProblems.Taken(rename.Target, rename.NewTarget)]);
            }
        }
        return null;
    }

    private static List<Reference> Rewrites(IReadOnlyList<Change> changes, Dictionary<ResourceRef, StoredResource> before)
    {
        var renames = changes.OfType<Rename>().Select(rename => rename.Target).ToList();
        return renames.Count == 0
            ? []
            : [.. ReferenceWalker.From(ResourceKinds.Assemble(before.Values)).Where(reference => renames.Contains(reference.To))];
    }

    private static Dictionary<ResourceRef, StoredResource> Applied(
        IReadOnlyList<Change> changes,
        Dictionary<ResourceRef, StoredResource> before,
        List<Reference> rewritten
    )
    {
        var after = new Dictionary<ResourceRef, StoredResource>(before);
        foreach (var change in changes)
        {
            switch (change)
            {
                case Put put:
                    if (put.Record.GetType() != ResourceKinds.RecordType(put.Target.Kind))
                    {
                        throw new ArgumentException(
                            $"A {put.Target.Kind} is a {ResourceKinds.RecordType(put.Target.Kind).Name}, not a {put.Record.GetType().Name}."
                        );
                    }
                    before.TryGetValue(put.Target, out var was);
                    after[put.Target] = new StoredResource(was?.Id ?? Guid.NewGuid(), put.Target, (was?.Revision ?? 0) + 1, put.Record);
                    break;
                case Edit edit:
                    var stored = before[edit.Target];
                    after[edit.Target] = stored with { Revision = stored.Revision + 1, Record = edit.Merge(stored.Record) };
                    break;
                case Remove remove:
                    after.Remove(remove.Target);
                    break;
                case Rename rename:
                    foreach (var from in rewritten.Where(found => found.To == rename.Target).Select(found => found.From).Distinct())
                    {
                        var referrer = after[from];
                        after[from] = referrer with
                        {
                            Revision = before[from].Revision + 1,
                            Record = ResourceKinds.Rewritten(referrer.Record, rename.Target, rename.NewId),
                        };
                    }
                    var moved = after[rename.Target];
                    after.Remove(rename.Target);
                    after[rename.NewTarget] = moved with
                    {
                        Target = rename.NewTarget,
                        Revision = before[rename.Target].Revision + 1,
                        Record = ResourceKinds.WithId(moved.Record, rename.NewId),
                    };
                    break;
                default:
                    throw new ArgumentException($"{change.GetType().Name} is not a known change.");
            }
        }
        return after;
    }
}
