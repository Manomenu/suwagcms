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

        if (Unmet(changes, before) is { } unmet)
        {
            return unmet;
        }
        if (Conflict(changes, before) is { } conflict)
        {
            return conflict;
        }
        var rewritten = Rewrites(changes, before);
        var after = Applied(changes, before, rewritten);
        // Why: one pass at the Version stage finds both kinds, since it runs every Save rule too; only the Save ones refuse.
        var assembled = ResourceKinds.Assemble(after.Values);
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

    // Why: expected revisions are checked against the state found under the lock, before any change is applied, so a stale edit loses to no mistake.
    private static EditResult? Unmet(IReadOnlyList<Change> changes, Dictionary<ResourceRef, StoredResource> before)
    {
        foreach (var change in changes)
        {
            before.TryGetValue(change.Target, out var found);
            if (change is Remove or Rename && found is null)
            {
                return new EditResult.Missing(change.Target);
            }
            if (change.ExpectedRevision is { } expected && expected != (found?.Revision ?? 0))
            {
                return new EditResult.Stale(change.Target, expected, found?.Revision);
            }
        }
        return null;
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
