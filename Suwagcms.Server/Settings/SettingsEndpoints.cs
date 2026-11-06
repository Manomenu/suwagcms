using Suwagcms.Contracts.Settings;

namespace Suwagcms.Server.Settings;

internal static class SettingsEndpoints
{
    public static void MapSettings(this IEndpointRouteBuilder routes, int pageSize) =>
        routes.MapGet("/settings", () => TypedResults.Ok(new ServerSettings(pageSize)));
}
