namespace Suwagcms.Sdk.Validation.Rules;

internal static class FactNames
{
    public static bool IsValid(string? name) => name is not null && name.Split('.').All(ResourceIds.IsCamelCase);

    public static string Suggest(string? name) => string.Join('.', (name ?? "").Split('.').Select(ResourceIds.Suggest));
}
