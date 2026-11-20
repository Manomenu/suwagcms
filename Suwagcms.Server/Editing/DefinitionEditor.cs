using Npgsql;
using Suwagcms.Sdk.Format.Actions;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;
using Suwagcms.Sdk.Validation.Guards;

namespace Suwagcms.Server.Editing;

internal sealed class DefinitionEditor(NpgsqlDataSource dataSource, TimeProvider time)
{
    public async Task<EditResult> ApplyAsync(string bot, IReadOnlyList<Change> changes, bool dryRun, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ResourceStore.LockBotAsync(connection, transaction, bot, cancellationToken);
        if (!await ResourceStore.BotExistsAsync(connection, transaction, bot, cancellationToken))
        {
            return new EditResult.UnknownBot();
        }
        var before = (await ResourceStore.LoadAsync(connection, transaction, bot, cancellationToken)).ToDictionary(item => item.Target);

        var readOnly = changes.Where(change => before.GetValueOrDefault(change.Target)?.Record is BotAction { IsCode: true }).ToList();
        if (readOnly.Count > 0)
        {
            return new EditResult.Refused([.. readOnly.Select(change => EditProblems.ReadOnly(change.Target))]);
        }

        var twice = Twice(changes);
        var unmet = Unmet(changes, before, twice);
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
        var assembled = ResourceKinds.Assemble(
            after
                .Values.OrderBy(item => ResourceKinds.Column(item.Target.Kind), StringComparer.Ordinal)
                .ThenBy(item => item.Target.Id, StringComparer.Ordinal)
        );
        var found = DefinitionValidator.Validate(assembled, ValidationStage.Version);
        var edited = EditGuard.Reshape(
            ResourceKinds.Assemble(before.Values),
            assembled,
            [.. WithoutEchoes([.. found.Where(mistake => mistake.Stage == ValidationStage.Save)], changes, rewritten)]
        );
        if (edited.Count > 0)
        {
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
        return new EditResult.Saved(revisions, !dryRun, found)
        {
            Rewritten = rewritten,
            Before = before,
            After = after,
        };
    }

    public async Task AlignCodeActionsAsync(string bot, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
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
        var known = before.Values.Select(item => item.Id).ToHashSet();
        var rows = after.Values.ToDictionary(item => item.Id);
        foreach (var gone in before.Values.Where(item => !rows.ContainsKey(item.Id)).Where(_ => !dryRun))
        {
            await ResourceStore.CountAvailabilityChangeAsync(connection, transaction, bot, gone.Id, cancellationToken);
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
            foreach (var was in before.Values.Where(item => rows.TryGetValue(item.Id, out var row) && row.Target != item.Target))
            {
                await ResourceStore.RememberFormerIdAsync(connection, transaction, bot, was, cancellationToken);
                await ResourceStore.CountAvailabilityChangeAsync(connection, transaction, bot, was.Id, cancellationToken);
            }
            foreach (var entry in ChangeEntries.Of(before, after))
            {
                await HistoryStore.WriteAsync(connection, transaction, bot, entry, now, cancellationToken);
            }
        }
        return revisions;
    }

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

    private static Dictionary<ResourceRef, List<int>> Twice(IReadOnlyList<Change> changes) =>
        changes
            .Select((change, index) => (change.Target, Position: index + 1))
            .GroupBy(item => item.Target)
            .Where(group => group.Count() > 1)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Position).ToList());

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
                    after[put.Target] = Stored(put.Target, put.Record, before.GetValueOrDefault(put.Target));
                    break;
                case Replace replace:
                    var found = before.GetValueOrDefault(replace.Target);
                    if (found is null || !ResourceKinds.Same(found.Record, replace.Record))
                    {
                        after[replace.Target] = Stored(replace.Target, replace.Record, found);
                    }
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

    private static StoredResource Stored(ResourceRef target, object record, StoredResource? was)
    {
        if (record.GetType() != ResourceKinds.RecordType(target.Kind))
        {
            throw new ArgumentException(
                $"A {target.Kind} is a {ResourceKinds.RecordType(target.Kind).Name}, not a {record.GetType().Name}."
            );
        }
        return new StoredResource(was?.Id ?? Guid.NewGuid(), target, (was?.Revision ?? 0) + 1, record);
    }
}
