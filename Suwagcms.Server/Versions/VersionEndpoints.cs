using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Versions;

internal static class VersionEndpoints
{
    public const string Missing = "version.missing";
    private const string Title = "The version was not created.";
    private const string Problem = "application/problem+json";

    public static void MapVersions(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bots/{bot}/versions", List).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes.MapGet("/bots/{bot}/versions/{number:int}", Get).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes
            .MapPost("/bots/{bot}/versions", Create)
            .Produces<VersionCreated>(StatusCodes.Status201Created)
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem)
            .Produces<Refusal>(StatusCodes.Status422UnprocessableEntity, Problem);
        routes.MapGet("/bots/{bot}/versions/status", Status).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes
            .MapPost("/bots/{bot}/versions/{number:int}/push", Push)
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem)
            .Produces<Refusal>(StatusCodes.Status422UnprocessableEntity, Problem);
    }

    private static async Task<Results<Ok<BotStatus>, ProblemHttpResult>> Status(
        string bot,
        BotStatusReader reader,
        CancellationToken cancellationToken
    ) =>
        await reader.ReadAsync(bot, cancellationToken) is { } status
            ? TypedResults.Ok(status)
            : EditProblems.For(new EditResult.UnknownBot(), bot, "The bot's state was not read.");

    private static async Task<Results<Ok<PushResult>, ProblemHttpResult>> Push(
        string bot,
        int number,
        VersionPusher pusher,
        CancellationToken cancellationToken
    )
    {
        var (result, rejected) = await pusher.PushAsync(bot, number, cancellationToken);
        return result is not null
            ? TypedResults.Ok(result)
            : TypedResults.Problem(
                title: "The version was not pushed.",
                detail: "1 mistake(s) in `mistakes`: fix each as its `fix` says, then try again.",
                statusCode: rejected?.Status ?? throw new InvalidOperationException("a push gave neither a result nor a refusal"),
                extensions: new Dictionary<string, object?> { ["mistakes"] = new[] { rejected.Mistake } }
            );
    }

    private static async Task<Results<Ok<List<VersionSummary>>, ProblemHttpResult>> List(
        string bot,
        Pool pool,
        CancellationToken cancellationToken
    ) =>
        await VersionStore.ListAsync(pool, bot, cancellationToken) is { } versions
            ? TypedResults.Ok(versions)
            : EditProblems.For(new EditResult.UnknownBot(), bot, "The versions were not listed.");

    private static async Task<Results<Ok<VersionDetail>, ProblemHttpResult>> Get(
        string bot,
        int number,
        Pool pool,
        CancellationToken cancellationToken
    )
    {
        if (await VersionStore.FindAsync(pool, bot, number, cancellationToken) is { } found)
        {
            return TypedResults.Ok(found);
        }
        // Why: an unknown bot and an unknown number are told apart, since the fix differs.
        return await VersionStore.ListAsync(pool, bot, cancellationToken) is null
            ? EditProblems.For(new EditResult.UnknownBot(), bot, "The version was not shown.")
            : TypedResults.Problem(
                title: "The version was not shown.",
                detail: "1 mistake(s) in `mistakes`: fix each as its `fix` says, then try again.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?> { ["mistakes"] = new[] { NoSuchVersion(bot, number) } }
            );
    }

    private static async Task<Results<Created<VersionCreated>, Ok<VersionCreated>, ProblemHttpResult>> Create(
        string bot,
        VersionCreator creator,
        CancellationToken cancellationToken,
        bool dryRun = false
    )
    {
        var (created, refusal) = await creator.CreateAsync(bot, dryRun, cancellationToken);
        if (created is null)
        {
            return EditProblems.For(
                refusal ?? throw new InvalidOperationException("a creation gave neither a version nor a refusal"),
                bot,
                Title
            );
        }
        return dryRun
            ? TypedResults.Ok(created)
            : TypedResults.Created($"/api/bots/{Uri.EscapeDataString(bot)}/versions/{created.Version.Number}", created);
    }

    internal static Mistake NoSuchVersion(string bot, int number) =>
        new(
            Missing,
            ValidationStage.Save,
            new ResourceRef(ResourceKind.Definition, bot),
            "number",
            $"Bot '{bot}' has no version {number}.",
            "List the bot's versions to see the numbers that exist.",
            []
        );
}
