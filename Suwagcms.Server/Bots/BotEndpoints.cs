using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using Suwagcms.Contracts.Bots;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Bots;

internal static class BotEndpoints
{
    public static void MapBots(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bots", List);
        routes
            .MapPost("/bots", Register)
            .ProducesRefusal(StatusCodes.Status422UnprocessableEntity)
            .ProducesRefusal(StatusCodes.Status409Conflict);
        routes.MapGet("/bots/{bot}/declaration", Declaration).ProducesRefusal(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<DeclarationStatus>, ProblemHttpResult>> Declaration(
        string bot,
        DeclarationAsker asker,
        CancellationToken cancellationToken,
        bool ask = true
    ) =>
        await (ask ? asker.AskAsync(bot, cancellationToken) : asker.ReadStoredAsync(bot, cancellationToken)) is { } status
            ? TypedResults.Ok(status)
            : EditProblems.For(new EditResult.UnknownBot(), bot, "The bot's declaration was not read.");

    private static async Task<Ok<List<Bot>>> List(NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
        TypedResults.Ok(await BotStore.ListAsync(dataSource, cancellationToken));

    private static async Task<Results<Created<Bot>, ProblemHttpResult>> Register(
        RegisterBot request,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken
    )
    {
        var mistakes = BotMistakes.In(request);
        if (mistakes.Count > 0)
        {
            return EditProblems.Refuse(
                StatusCodes.Status422UnprocessableEntity,
                "The bot was not registered.",
                RefusalCode.BotInvalid,
                mistakes
            );
        }
        var bot = new Bot(request.Id, request.Name.Trim(), request.Address.Trim());
        if (!await BotStore.AddAsync(dataSource, bot, cancellationToken))
        {
            return EditProblems.Refuse(
                StatusCodes.Status409Conflict,
                "The bot was not registered.",
                RefusalCode.BotTaken,
                [BotMistakes.Taken(bot.Id)]
            );
        }
        return TypedResults.Created((string?)null, bot);
    }
}
