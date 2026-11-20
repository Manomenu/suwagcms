using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using Suwagcms.Contracts;
using Suwagcms.Contracts.Bots;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Bots;

internal static class BotEndpoints
{
    public static void MapBots(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bots", List);
        routes
            .MapPost("/bots", Register)
            .Produces<Refusal>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")
            .Produces<Refusal>(StatusCodes.Status409Conflict, "application/problem+json");
        routes.MapGet("/bots/{bot}/declaration", Declaration).Produces<Refusal>(StatusCodes.Status404NotFound, "application/problem+json");
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
            return Refused(StatusCodes.Status422UnprocessableEntity, "The bot was not registered.", mistakes);
        }
        var bot = new Bot(request.Id, request.Name.Trim(), request.Address.Trim());
        if (!await BotStore.AddAsync(dataSource, bot, cancellationToken))
        {
            return Refused(StatusCodes.Status409Conflict, "The bot was not registered.", [BotMistakes.Taken(bot.Id)]);
        }
        return TypedResults.Created((string?)null, bot);
    }

    private static ProblemHttpResult Refused(int status, string title, List<Mistake> mistakes) =>
        TypedResults.Problem(
            title: title,
            detail: $"{mistakes.Count} mistake(s) in `mistakes`: fix each as its `fix` says, then register again.",
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["mistakes"] = mistakes }
        );
}
