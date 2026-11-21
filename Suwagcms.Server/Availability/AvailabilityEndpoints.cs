using Microsoft.AspNetCore.Http.HttpResults;
using Npgsql;
using Suwagcms.Contracts.Availability;
using Suwagcms.Sdk.Format.Procedures;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Bots;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Availability;

internal static class AvailabilityEndpoints
{
    private const string WhatCanBeOff =
        "Only an article, a procedure or an action can be turned off; facts and knowledge bases have no availability.";

    private static readonly Dictionary<string, ResourceKind> Kinds = new(StringComparer.Ordinal)
    {
        ["article"] = ResourceKind.Article,
        ["procedure"] = ResourceKind.Procedure,
        ["action"] = ResourceKind.Action,
    };

    public static void MapAvailability(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bots/{bot}/availability", Show).ProducesRefusal(StatusCodes.Status404NotFound);
        routes.MapPost("/bots/{bot}/availability/send", Send).ProducesRefusal(StatusCodes.Status404NotFound);
        routes
            .MapPost("/bots/{bot}/availability/{kind}/{id}/off", TurnOff)
            .ProducesRefusal(StatusCodes.Status404NotFound)
            .ProducesRefusal(StatusCodes.Status422UnprocessableEntity);
        routes
            .MapPost("/bots/{bot}/availability/{kind}/{id}/on", TurnOn)
            .ProducesRefusal(StatusCodes.Status404NotFound)
            .ProducesRefusal(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<Results<Ok<AvailabilityView>, ProblemHttpResult>> Show(
        string bot,
        NpgsqlDataSource dataSource,
        AvailabilitySender sender,
        CancellationToken cancellationToken,
        bool ask = true
    )
    {
        if (await BotStore.FindAddressAsync(dataSource, bot, cancellationToken) is not { } address)
        {
            return EditProblems.For(new EditResult.UnknownBot(), bot, "The availability was not shown.");
        }
        var resources = await AvailabilityStore.ListAsync(dataSource, bot, Kinds.Values, cancellationToken);
        var delivery = ask
            ? await sender.AskAsync(bot, address, cancellationToken)
            : new AvailabilityDelivery(
                AvailabilityDeliveryState.NotAsked,
                await AvailabilityStore.ReadNumberAsync(dataSource, bot, cancellationToken),
                null,
                null
            );
        return TypedResults.Ok(new AvailabilityView(delivery.Number, resources, delivery));
    }

    private static async Task<Results<Ok<AvailabilityDelivery>, ProblemHttpResult>> Send(
        string bot,
        NpgsqlDataSource dataSource,
        AvailabilitySender sender,
        CancellationToken cancellationToken
    ) =>
        await BotStore.FindAddressAsync(dataSource, bot, cancellationToken) is { } address
            ? TypedResults.Ok(await sender.SendAsync(bot, address, cancellationToken))
            : EditProblems.For(new EditResult.UnknownBot(), bot, "The availability was not sent.");

    private static Task<Results<Ok<AvailabilityAnswer>, ProblemHttpResult>> TurnOff(
        string bot,
        string kind,
        string id,
        NpgsqlDataSource dataSource,
        AvailabilitySender sender,
        TimeProvider time,
        CancellationToken cancellationToken,
        bool dryRun = false
    ) => TurnAsync(bot, kind, id, off: true, dryRun, dataSource, sender, time, cancellationToken);

    private static Task<Results<Ok<AvailabilityAnswer>, ProblemHttpResult>> TurnOn(
        string bot,
        string kind,
        string id,
        NpgsqlDataSource dataSource,
        AvailabilitySender sender,
        TimeProvider time,
        CancellationToken cancellationToken,
        bool dryRun = false
    ) => TurnAsync(bot, kind, id, off: false, dryRun, dataSource, sender, time, cancellationToken);

    private static async Task<Results<Ok<AvailabilityAnswer>, ProblemHttpResult>> TurnAsync(
        string bot,
        string kind,
        string id,
        bool off,
        bool dryRun,
        NpgsqlDataSource dataSource,
        AvailabilitySender sender,
        TimeProvider time,
        CancellationToken cancellationToken
    )
    {
        var title = off ? "The resource was not turned off." : "The resource was not turned on.";
        if (!Kinds.TryGetValue(kind, out var resourceKind))
        {
            return EditProblems.For(new EditResult.Refused([NoAvailability(kind)]), bot, title);
        }
        var target = new ResourceRef(resourceKind, id);
        if (target == new ResourceRef(ResourceKind.Procedure, Procedure.ConversationId))
        {
            return EditProblems.For(new EditResult.Refused([Conversation(target)]), bot, title);
        }
        var (result, usedBy) = await AvailabilityStore.ChangeAsync(
            dataSource,
            bot,
            target,
            off,
            dryRun,
            time.GetUtcNow(),
            cancellationToken
        );
        if (result == AvailabilityStore.Outcome.UnknownBot)
        {
            return EditProblems.For(new EditResult.UnknownBot(), bot, title);
        }
        if (result == AvailabilityStore.Outcome.Missing)
        {
            return Missing(target, bot, title);
        }
        var address = await BotStore.FindAddressAsync(dataSource, bot, cancellationToken) ?? "";
        var delivery = dryRun
            ? new AvailabilityDelivery(
                AvailabilityDeliveryState.NotAsked,
                await AvailabilityStore.ReadNumberAsync(dataSource, bot, cancellationToken),
                null,
                null
            )
            : await sender.SendAsync(bot, address, cancellationToken);
        return TypedResults.Ok(new AvailabilityAnswer(kind, id, off, result == AvailabilityStore.Outcome.Changed, usedBy, delivery));
    }

    private static Mistake NoAvailability(string kind) =>
        new(
            MistakeCode.AvailabilityKindRefused,
            ValidationStage.Save,
            new ResourceRef(ResourceKind.Definition, kind),
            "kind",
            $"'{kind}' cannot be turned on or off.",
            WhatCanBeOff,
            []
        );

    private static Mistake Conversation(ResourceRef target) =>
        new(
            MistakeCode.AvailabilityConversationRefused,
            ValidationStage.Save,
            target,
            "id",
            "The procedure 'conversation' cannot be turned off: every conversation is based on it.",
            "Turn off the procedures or the articles it uses instead.",
            []
        );

    private static ProblemHttpResult Missing(ResourceRef target, string bot, string title)
    {
        var missing = EditProblems.Missing(target, bot);
        return EditProblems.Refuse(
            StatusCodes.Status404NotFound,
            title,
            RefusalCode.ResourceMissing,
            [missing with { Fix = $"{missing.Fix} {WhatCanBeOff}" }]
        );
    }
}
