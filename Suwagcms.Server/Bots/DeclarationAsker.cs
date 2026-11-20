using System.Globalization;
using System.Text.Json;
using Npgsql;
using Suwagcms.Contracts.Bots;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Bots;

internal sealed class DeclarationAsker(NpgsqlDataSource dataSource, BotClient client, TimeProvider time, DefinitionEditor editor)
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public async Task<DeclarationStatus?> ReadStoredAsync(string bot, CancellationToken cancellationToken)
    {
        return await DeclarationStore.FindAsync(dataSource, bot, cancellationToken) is { } row
            ? await StatusAsync(bot, row, DeclarationAsk.NotAsked, null, [], true, cancellationToken)
            : null;
    }

    public async Task<DeclarationStatus?> AskAsync(string bot, CancellationToken cancellationToken) =>
        await AskAsync(bot, listCannotRun: true, cancellationToken);

    public async Task<DeclarationStatus?> AskForPushAsync(string bot, CancellationToken cancellationToken) =>
        await AskAsync(bot, listCannotRun: false, cancellationToken);

    private async Task<DeclarationStatus?> AskAsync(string bot, bool listCannotRun, CancellationToken cancellationToken)
    {
        if (await DeclarationStore.FindAsync(dataSource, bot, cancellationToken) is not { } row)
        {
            return null;
        }

        switch (await client.ReadDeclarationAsync(row.Address, cancellationToken))
        {
            case BotClient.Reply.Silent silent:
                return await StatusAsync(bot, row, DeclarationAsk.Unreachable, silent.Reason, [], listCannotRun, cancellationToken);
            case BotClient.Reply.Answer { Status: StatusCodes.Status404NotFound }:
                return await StoredAnswerAsync(bot, row.Address, null, listCannotRun, cancellationToken);
            case BotClient.Reply.Answer { Status: StatusCodes.Status200OK } answer:
                return await JudgeAsync(bot, row, answer.Body, listCannotRun, cancellationToken);
            case BotClient.Reply.Answer answer:
                var reason = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{row.Address} answered {answer.Status}, which is not the answer of a bot's declaration route"
                );
                return await StatusAsync(bot, row, DeclarationAsk.Unreachable, reason, [], listCannotRun, cancellationToken);
            default:
                throw new InvalidOperationException("a bot's reply is an answer or silence");
        }
    }

    private async Task<DeclarationStatus> JudgeAsync(
        string bot,
        DeclarationStore.StoredDeclaration row,
        string body,
        bool listCannotRun,
        CancellationToken cancellationToken
    )
    {
        var (declaration, unreadable) = DeclarationValidator.ParseJson(body);
        var mistakes = declaration is null ? unreadable : DeclarationValidator.CheckAnswer(declaration, bot);
        if (mistakes.Count > 0 || declaration is null)
        {
            return await StatusAsync(
                bot,
                row,
                DeclarationAsk.Invalid,
                "the bot's answer was refused",
                mistakes,
                listCannotRun,
                cancellationToken
            );
        }
        return await StoredAnswerAsync(
            bot,
            row.Address,
            declaration.DeclaresNothing ? null : declaration,
            listCannotRun,
            cancellationToken
        );
    }

    private async Task<DeclarationStatus> StoredAnswerAsync(
        string bot,
        string address,
        BotDeclaration? declaration,
        bool listCannotRun,
        CancellationToken cancellationToken
    )
    {
        var text = declaration is null ? null : JsonSerializer.Serialize(declaration, Web);
        var at = time.GetUtcNow();
        await DeclarationStore.StoreAsync(dataSource, bot, text, at, cancellationToken);
        await editor.AlignCodeActionsAsync(bot, cancellationToken);
        return await StatusAsync(
            bot,
            new DeclarationStore.StoredDeclaration(address, text, at.UtcDateTime),
            DeclarationAsk.Answered,
            null,
            [],
            listCannotRun,
            cancellationToken
        );
    }

    private async Task<DeclarationStatus> StatusAsync(
        string bot,
        DeclarationStore.StoredDeclaration row,
        DeclarationAsk ask,
        string? reason,
        IReadOnlyList<Mistake> mistakes,
        bool listCannotRun,
        CancellationToken cancellationToken
    )
    {
        var stored =
            row.AnsweredAt is null ? DeclarationStored.NeverAnswered
            : row.Declaration is null ? DeclarationStored.DeclaresNothing
            : DeclarationStored.Declared;
        var declaration = row.Parsed;
        return new DeclarationStatus(
            bot,
            row.Address,
            ask,
            stored,
            declaration,
            row.AnsweredAt,
            reason,
            mistakes,
            FormatFix(declaration),
            declaration is null || !listCannotRun ? [] : await CannotRunAsync(bot, declaration, cancellationToken)
        );
    }

    private static string? FormatFix(BotDeclaration? declaration) =>
        declaration?.FormatVersion is { } wanted && wanted != BotDefinition.CurrentFormatVersion
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"The bot wants format {wanted} and this CMS reads format {BotDefinition.CurrentFormatVersion}: update Suwagcms.Sdk on the CMS, or declare format {BotDefinition.CurrentFormatVersion} in the bot."
            )
            : null;

    private async Task<IReadOnlyList<Mistake>> CannotRunAsync(string bot, BotDeclaration declaration, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var resources = await ResourceStore.LoadAsync(connection, null, bot, cancellationToken);
        return
        [
            .. DeclarationValidator
                .CheckDefinition(ResourceKinds.Assemble(resources), declaration)
                .Where(mistake =>
                    mistake.Code
                        is not (MistakeCodes.DeclarationFormatVersion or MistakeCodes.PersonaRequired or MistakeCodes.PersonaIncomplete)
                ),
        ];
    }
}
