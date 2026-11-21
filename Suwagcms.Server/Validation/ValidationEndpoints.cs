using Microsoft.AspNetCore.Http.HttpResults;
using Suwagcms.Contracts.Validation;
using Suwagcms.Sdk.Format.Declaration;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Editing;

namespace Suwagcms.Server.Validation;

internal static class ValidationEndpoints
{
    public static void MapValidation(this IEndpointRouteBuilder routes) =>
        routes
            .MapPost("/validate", Validate)
            .Accepts<ValidateDefinition>("application/json")
            .ProducesRefusal(StatusCodes.Status422UnprocessableEntity);

    private static async Task<Results<Ok<ValidationAnswer>, ProblemHttpResult>> Validate(
        HttpRequest request,
        CancellationToken cancellationToken
    )
    {
        var (body, unreadable) = await StrictBody.ReadAsync<ValidateDefinition>(
            request.Body,
            new ResourceRef(ResourceKind.Definition, string.Empty),
            cancellationToken
        );
        if (body is null)
        {
            return EditProblems.For(new EditResult.Refused(unreadable), string.Empty, "The definition was not validated.");
        }

        BotDeclaration? declaration = null;
        if (body.Declaration is { } sent)
        {
            var (parsed, mistakes) = DeclarationValidator.ParseJson(sent.GetRawText());
            if (parsed is null)
            {
                return TypedResults.Ok(new ValidationAnswer(mistakes));
            }
            declaration = parsed;
        }

        var json = body.Definition.ValueKind == System.Text.Json.JsonValueKind.Undefined ? "" : body.Definition.GetRawText();
        return TypedResults.Ok(
            new ValidationAnswer(DefinitionValidator.ValidateJson(json, body.Stage ?? ValidationStage.Version, declaration))
        );
    }
}
