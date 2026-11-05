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
        var after = Applied(changes, before);
        // Why: one pass at the Version stage finds both kinds, since it runs every Save rule too; only the Save ones refuse. The order is fixed so that a list means the same in any order.
        var assembled = ResourceKinds.Assemble(
            after
                .Values.OrderBy(item => ResourceKinds.Column(item.Target.Kind), StringComparer.Ordinal)
                .ThenBy(item => item.Target.Id, StringComparer.Ordinal)
        );
        var found = DefinitionValidator.Validate(assembled, ValidationStage.Version);
        if (found.Where(mistake => mistake.Stage == ValidationStage.Save).ToList() is { Count: > 0 } mistakes)
        {
            // Why: a delete that leaves references behind is refused by the same rule as a typo, worded for the resource being deleted.
            var removed = changes.OfType<Remove>().Select(change => change.Target).ToList();
            return new EditResult.Refused(DeleteGuard.Reshape(assembled, removed, mistakes));
        }

        var touched = changes.Select(change => change.Target).Distinct().ToList();
        var revisions = new Dictionary<ResourceRef, int>();
        var now = time.GetUtcNow();
        foreach (var target in touched)
        {
            before.TryGetValue(target, out var was);
            if (!after.TryGetValue(target, out var becomes))
            {
                if (was is not null && !dryRun)
                {
                    await ResourceStore.DeleteAsync(connection, transaction, was.Id, cancellationToken);
                }
            }
            else if (was is null)
            {
                revisions[target] = becomes.Revision;
                if (!dryRun)
                {
                    await ResourceStore.InsertAsync(connection, transaction, bot, becomes, now, cancellationToken);
                }
            }
            else
            {
                revisions[target] = becomes.Revision;
                if (!dryRun)
                {
                    await ResourceStore.UpdateAsync(connection, transaction, becomes, now, cancellationToken);
                }
            }
        }

        if (dryRun)
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        else
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return new EditResult.Saved(revisions, !dryRun, found);
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
            if (change is Remove or Edit && found is null)
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

    private static Dictionary<ResourceRef, StoredResource> Applied(
        IReadOnlyList<Change> changes,
        Dictionary<ResourceRef, StoredResource> before
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
                default:
                    throw new ArgumentException($"{change.GetType().Name} is not a known change.");
            }
        }
        return after;
    }
}
