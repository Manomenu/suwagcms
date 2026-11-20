using System.Globalization;
using System.Text.Json;
using Suwagcms.Contracts.History;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Bots;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Versions;

internal sealed class VersionCreator(Pool pool, TimeProvider time)
{
    public const string Unchanged = "version.unchanged";

    public async Task<(VersionCreated? Created, EditResult? Refusal)> CreateAsync(
        string bot,
        bool dryRun,
        CancellationToken cancellationToken
    )
    {
        await using var connection = await pool.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await ResourceStore.LockBotAsync(connection, transaction, bot, cancellationToken);
        if (!await ResourceStore.BotExistsAsync(connection, transaction, bot, cancellationToken))
        {
            return (null, new EditResult.UnknownBot());
        }
        var resources = await ResourceStore.LoadAsync(connection, transaction, bot, cancellationToken);

        var declared = await DeclarationStore.FindAsync(connection, transaction, bot, cancellationToken);
        var mistakes = DefinitionValidator.Validate(ResourceKinds.Assemble(resources), ValidationStage.Version, declared?.Parsed);
        if (mistakes.Count > 0)
        {
            return (null, new EditResult.Refused(mistakes));
        }

        var latest = await VersionStore.LatestAsync(connection, transaction, bot, cancellationToken);
        if (latest is { } last && VersionDocument.Write(bot, last.Number, resources).Sha256 == last.Sha256)
        {
            return (null, new EditResult.Refused([NothingChanged(bot, last.Number)]));
        }

        var number = (latest?.Number ?? 0) + 1;
        var (document, sha256) = VersionDocument.Write(bot, number, resources);
        var now = time.GetUtcNow();
        if (!dryRun)
        {
            await VersionStore.InsertAsync(connection, transaction, bot, number, document, sha256, now, cancellationToken);
            await HistoryStore.WriteAsync(
                connection,
                transaction,
                bot,
                new HistoryStore.Recorded(
                    HistoryStore.VersionKind,
                    number.ToString(CultureInfo.InvariantCulture),
                    "created",
                    null,
                    [new FieldChange("sha256", null, JsonSerializer.SerializeToElement(sha256))]
                ),
                now,
                cancellationToken
            );
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        var summary = new VersionSummary(
            number,
            now,
            resources.Count(item => item.Target.Kind == ResourceKind.GlobalFact),
            resources.Count(item => item.Target.Kind == ResourceKind.KnowledgeBase),
            resources.Count(item => item.Target.Kind == ResourceKind.Article),
            resources.Count(item => item.Target.Kind == ResourceKind.Procedure),
            resources.Count(item => item.Target.Kind == ResourceKind.Action),
            sha256
        );
        return (new VersionCreated(summary, !dryRun), null);
    }

    private static Mistake NothingChanged(string bot, int latest) =>
        new(
            Unchanged,
            ValidationStage.Version,
            new ResourceRef(ResourceKind.Definition, bot),
            "definition",
            string.Create(CultureInfo.InvariantCulture, $"Nothing has changed since version {latest} of bot '{bot}'."),
            "Change a fact, an article or a procedure, then create the version again; the latest version already holds this state.",
            []
        );
}
