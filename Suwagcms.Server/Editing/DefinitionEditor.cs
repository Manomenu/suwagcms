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
        var after = Applied(changes, before);
        // Why: one pass at the Version stage finds both kinds, since it runs every Save rule too; only the Save ones refuse.
        var found = DefinitionValidator.Validate(ResourceKinds.Assemble(after.Values), ValidationStage.Version);
        if (found.Where(mistake => mistake.Stage == ValidationStage.Save).ToList() is { Count: > 0 } mistakes)
        {
            return new EditResult.Refused(mistakes);
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

    // Why: expected revisions are checked against the state found under the lock, before any change is applied, so a stale edit loses to no mistake.
    private static EditResult? Unmet(IReadOnlyList<Change> changes, Dictionary<ResourceRef, StoredResource> before)
    {
        foreach (var change in changes)
        {
            before.TryGetValue(change.Target, out var found);
            if (change is Remove && found is null)
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
