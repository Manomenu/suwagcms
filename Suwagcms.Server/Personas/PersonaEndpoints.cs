using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Contracts;
using Suwagcms.Sdk.Format;
using Suwagcms.Server.Database;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Personas;

// Why: a bot has one persona from the start, so there is no list, add or delete, and its address holds no id.
internal static class PersonaEndpoints
{
    private const string Title = "The persona was not saved.";
    private const string Problem = "application/problem+json";

    public static void MapPersona(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/bots/{bot}/persona", Get).Produces<Refusal>(StatusCodes.Status404NotFound, Problem);
        routes
            .MapPut("/bots/{bot}/persona", Save)
            .Accepts<SavePersona>("application/json")
            .Produces<Refusal>(StatusCodes.Status404NotFound, Problem)
            .Produces<Refusal>(StatusCodes.Status409Conflict, Problem)
            .Produces<Refusal>(StatusCodes.Status422UnprocessableEntity, Problem);
    }

    private static async Task<Results<Ok<PersonaView>, ProblemHttpResult>> Get(string bot, Pool pool, CancellationToken cancellationToken)
    {
        if (await ResourceStore.ReadAsync(pool, bot, cancellationToken) is not { } stored)
        {
            return EditProblems.For(new EditResult.UnknownBot(), bot, "The persona was not shown.");
        }
        var found = stored.SingleOrDefault(item => item.Target == PersonaMapping.Target);
        return TypedResults.Ok(PersonaMapping.Show((Persona?)found?.Record, found?.Revision ?? 0));
    }

    private static async Task<Results<Ok<PersonaView>, ProblemHttpResult>> Save(
        string bot,
        HttpRequest request,
        DefinitionEditor editor,
        CancellationToken cancellationToken,
        bool dryRun = false,
        int? ifRevision = null
    )
    {
        var target = PersonaMapping.Target;
        var (body, unreadable) = await StrictBody.ReadAsync(
            request.Body,
            target,
            root => PersonaMapping.Read(root, target),
            cancellationToken
        );
        if (body is null)
        {
            return EditProblems.For(new EditResult.Refused(unreadable), bot, Title);
        }
        var record = PersonaMapping.ToRecord(body);
        var result = await editor.ApplyAsync(bot, [new Put(target, record, ifRevision)], dryRun, cancellationToken);
        return result is EditResult.Saved saved
            ? TypedResults.Ok(PersonaMapping.Show(record, saved.Revisions[target]))
            : EditProblems.For(result, bot, Title);
    }
}
