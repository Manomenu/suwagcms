using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Contracts;
using Suwagcms.Contracts.Bots;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.Format.References;
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
        routes.MapGet("/bots/{bot}/versions/{from:int}/diff/{to:int}", Diff).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes.MapGet("/bots/{bot}/versions/{number:int}/export", Export).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes.MapGet("/bots/{bot}/versions/status", Status).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes.MapPost("/bots/{bot}/versions/{number:int}/push", Push).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes.MapGet("/bots/{bot}/versions/{number:int}/push-plan", PushPlan).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
    }

    // Why: it reads two frozen documents and neither the working state nor the bot, so the answer is the same for ever.
    private static async Task<Results<Ok<VersionComparison>, ProblemHttpResult>> Diff(
        string bot,
        int from,
        int to,
        Pool pool,
        CancellationToken cancellationToken
    )
    {
        var before = await VersionStore.FindAsync(pool, bot, from, cancellationToken);
        var after = await VersionStore.FindAsync(pool, bot, to, cancellationToken);
        if (before is not null && after is not null)
        {
            return TypedResults.Ok(VersionComparer.Compare(bot, from, to, before.Document, after.Document));
        }
        if (await VersionStore.ListAsync(pool, bot, cancellationToken) is null)
        {
            return EditProblems.For(new EditResult.UnknownBot(), bot, "The versions were not compared.");
        }
        var latest = await VersionStore.LatestNumberAsync(pool, bot, cancellationToken);
        var missing = new (int Number, VersionDetail? Found)[] { (from, before), (to, after) }
            .Where(side => side.Found is null)
            .Select(side => side.Number)
            .Distinct()
            .Select(number => NoSuchVersion(bot, number, latest))
            .ToList();
        return TypedResults.Problem(
            title: "The versions were not compared.",
            detail: $"{missing.Count} mistake(s) in `mistakes`: fix each as its `fix` says, then try again.",
            statusCode: StatusCodes.Status404NotFound,
            extensions: new Dictionary<string, object?> { ["mistakes"] = missing }
        );
    }

    // Why: it only reads the frozen document; writing the files to a folder is the client's business, and nothing takes them back.
    private static async Task<Results<Ok<VersionExport>, ProblemHttpResult>> Export(
        string bot,
        int number,
        Pool pool,
        CancellationToken cancellationToken
    ) =>
        await VersionStore.FindAsync(pool, bot, number, cancellationToken) is { } found
            ? TypedResults.Ok(VersionExportSplitter.Split(bot, number, found.Document))
            : await MissingAsync(pool, bot, number, "The version was not exported.", cancellationToken);

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
            : NotPushed(rejected ?? throw new InvalidOperationException("a push gave neither a result nor a refusal"));
    }

    // Why: a read like the page's status, so it answers what a push would do while changing neither the bot nor the record of attempts.
    private static async Task<Results<Ok<PushPlan>, ProblemHttpResult>> PushPlan(
        string bot,
        int number,
        VersionPusher pusher,
        CancellationToken cancellationToken
    )
    {
        var (plan, rejected) = await pusher.PlanAsync(bot, number, cancellationToken);
        return plan is not null
            ? TypedResults.Ok(plan)
            : NotPushed(rejected ?? throw new InvalidOperationException("a plan gave neither an answer nor a refusal"));
    }

    private static ProblemHttpResult NotPushed(VersionPusher.Rejected rejected) =>
        TypedResults.Problem(
            title: "The version was not pushed.",
            detail: "1 mistake(s) in `mistakes`: fix each as its `fix` says, then try again.",
            statusCode: rejected.Status,
            extensions: new Dictionary<string, object?> { ["mistakes"] = new[] { rejected.Mistake } }
        );

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
        return await MissingAsync(pool, bot, number, "The version was not shown.", cancellationToken);
    }

    // Why: an unknown bot and an unknown number are told apart, since the fix differs.
    internal static async Task<ProblemHttpResult> MissingAsync(
        Pool pool,
        string bot,
        int number,
        string title,
        CancellationToken cancellationToken
    ) =>
        await VersionStore.ListAsync(pool, bot, cancellationToken) is null
            ? EditProblems.For(new EditResult.UnknownBot(), bot, title)
            : TypedResults.Problem(
                title: title,
                detail: "1 mistake(s) in `mistakes`: fix each as its `fix` says, then try again.",
                statusCode: StatusCodes.Status404NotFound,
                extensions: new Dictionary<string, object?>
                {
                    ["mistakes"] = new[] { NoSuchVersion(bot, number, await VersionStore.LatestNumberAsync(pool, bot, cancellationToken)) },
                }
            );

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

    internal static Mistake NoSuchVersion(string bot, int number, int? latest) =>
        new(
            Missing,
            ValidationStage.Save,
            new ResourceRef(ResourceKind.Definition, bot),
            "number",
            latest is { } last
                ? $"Bot '{bot}' has no version {number}; its latest is {last}."
                : $"Bot '{bot}' has no version {number}, and no version yet.",
            "List the bot's versions to see the numbers that exist.",
            []
        );
}
