using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using Suwagcms.Contracts.Bots;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Versions;

internal static class VersionEndpoints
{
    public const string Missing = "version.missing";
    private const string Title = "The version was not created.";

    public static void MapVersions(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bots/{bot}/versions", List).ProducesRefusal(StatusCodes.Status404NotFound);
        routes.MapGet("/bots/{bot}/versions/{number:int}", Get).ProducesRefusal(StatusCodes.Status404NotFound);
        routes
            .MapPost("/bots/{bot}/versions", Create)
            .Produces<VersionCreated>(StatusCodes.Status201Created)
            .ProducesRefusal(StatusCodes.Status404NotFound)
            .ProducesRefusal(StatusCodes.Status422UnprocessableEntity);
        routes.MapGet("/bots/{bot}/versions/{from:int}/diff/{to:int}", Diff).ProducesRefusal(StatusCodes.Status404NotFound);
        routes.MapGet("/bots/{bot}/versions/{number:int}/export", Export).ProducesRefusal(StatusCodes.Status404NotFound);
        routes.MapGet("/bots/{bot}/versions/status", Status).ProducesRefusal(StatusCodes.Status404NotFound);
        routes.MapPost("/bots/{bot}/versions/{number:int}/push", Push).ProducesRefusal(StatusCodes.Status404NotFound);
        routes.MapGet("/bots/{bot}/versions/{number:int}/push-plan", PushPlan).ProducesRefusal(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<VersionComparison>, ProblemHttpResult>> Diff(
        string bot,
        int from,
        int to,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken
    )
    {
        var before = await VersionStore.FindAsync(dataSource, bot, from, cancellationToken);
        var after = await VersionStore.FindAsync(dataSource, bot, to, cancellationToken);
        if (before is not null && after is not null)
        {
            return TypedResults.Ok(VersionComparer.Compare(bot, from, to, before.Document, after.Document));
        }
        if (await VersionStore.ListAsync(dataSource, bot, cancellationToken) is null)
        {
            return EditProblems.For(new EditResult.UnknownBot(), bot, "The versions were not compared.");
        }
        var latest = await VersionStore.LatestNumberAsync(dataSource, bot, cancellationToken);
        var missing = new (int Number, VersionDetail? Found)[] { (from, before), (to, after) }
            .Where(side => side.Found is null)
            .Select(side => side.Number)
            .Distinct()
            .Select(number => NoSuchVersion(bot, number, latest))
            .ToList();
        return EditProblems.Refuse(StatusCodes.Status404NotFound, "The versions were not compared.", RefusalCode.VersionMissing, missing);
    }

    private static async Task<Results<Ok<VersionExport>, ProblemHttpResult>> Export(
        string bot,
        int number,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken
    ) =>
        await VersionStore.FindAsync(dataSource, bot, number, cancellationToken) is { } found
            ? TypedResults.Ok(VersionExportSplitter.Split(bot, number, found.Document))
            : await MissingAsync(dataSource, bot, number, "The version was not exported.", cancellationToken);

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
        EditProblems.Refuse(rejected.Status, "The version was not pushed.", RefusalCode.VersionNotPushed, [rejected.Mistake]);

    private static async Task<Results<Ok<List<VersionSummary>>, ProblemHttpResult>> List(
        string bot,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken
    ) =>
        await VersionStore.ListAsync(dataSource, bot, cancellationToken) is { } versions
            ? TypedResults.Ok(versions)
            : EditProblems.For(new EditResult.UnknownBot(), bot, "The versions were not listed.");

    private static async Task<Results<Ok<VersionDetail>, ProblemHttpResult>> Get(
        string bot,
        int number,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken
    )
    {
        if (await VersionStore.FindAsync(dataSource, bot, number, cancellationToken) is { } found)
        {
            return TypedResults.Ok(found);
        }
        return await MissingAsync(dataSource, bot, number, "The version was not shown.", cancellationToken);
    }

    internal static async Task<ProblemHttpResult> MissingAsync(
        NpgsqlDataSource dataSource,
        string bot,
        int number,
        string title,
        CancellationToken cancellationToken
    ) =>
        await VersionStore.ListAsync(dataSource, bot, cancellationToken) is null
            ? EditProblems.For(new EditResult.UnknownBot(), bot, title)
            : EditProblems.Refuse(
                StatusCodes.Status404NotFound,
                title,
                RefusalCode.VersionMissing,
                [NoSuchVersion(bot, number, await VersionStore.LatestNumberAsync(dataSource, bot, cancellationToken))]
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
