using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Database;

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
    }

    private static async Task<Ok<List<Bot>>> List(Pool pool, CancellationToken cancellationToken) =>
        TypedResults.Ok(await BotStore.ListAsync(pool, cancellationToken));

    private static async Task<Results<Created<Bot>, ProblemHttpResult>> Register(
        RegisterBot request,
        Pool pool,
        CancellationToken cancellationToken
    )
    {
        var mistakes = BotMistakes.In(request);
        if (mistakes.Count > 0)
        {
            return Refused(StatusCodes.Status422UnprocessableEntity, "The bot was not registered.", mistakes);
        }
        var bot = new Bot(request.Id, request.Name.Trim(), request.Address.Trim());
        if (!await BotStore.AddAsync(pool, bot, cancellationToken))
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
