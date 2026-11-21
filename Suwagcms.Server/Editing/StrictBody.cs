using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Server.Editing;

internal static class StrictBody
{
    private static readonly JsonSerializerOptions Strict = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        NumberHandling = JsonNumberHandling.Strict,
    };

    public static Task<(T? Body, IReadOnlyList<Mistake> Mistakes)> ReadAsync<T>(
        Stream body,
        ResourceRef target,
        CancellationToken cancellationToken
    )
        where T : class => ReadAsync(body, target, root => Read<T>(root, target), cancellationToken);

    public static async Task<(T? Body, IReadOnlyList<Mistake> Mistakes)> ReadAsync<T>(
        Stream body,
        ResourceRef target,
        Func<JsonElement, (T? Body, IReadOnlyList<Mistake> Mistakes)> read,
        CancellationToken cancellationToken
    )
        where T : class
    {
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            return (
                null,
                [Make(MistakeCode.SaveInvalid, target, "$", $"The body is not valid JSON: {exception.Message}", "Send one JSON object.")]
            );
        }

        using (document)
        {
            return read(document.RootElement);
        }
    }

    public static (T? Body, IReadOnlyList<Mistake> Mistakes) Read<T>(JsonElement root, ResourceRef target)
        where T : class
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return (null, [Make(MistakeCode.SaveInvalid, target, "$", "The body is not a JSON object.", "Send one JSON object.")]);
        }
        var known = Strict.GetTypeInfo(typeof(T)).Properties.Select(property => property.Name).ToList();
        var unknown = root.EnumerateObject()
            .Where(property => !known.Contains(property.Name, StringComparer.Ordinal))
            .Select(property => Unknown(target, property.Name, string.Join(", ", known)))
            .ToList();
        if (unknown.Count > 0)
        {
            return (null, unknown);
        }
        try
        {
            return (root.Deserialize<T>(Strict), []);
        }
        catch (JsonException exception)
        {
            return (
                null,
                [
                    Make(
                        MistakeCode.SaveInvalid,
                        target,
                        (exception.Path ?? "$").Replace("$.", "", StringComparison.Ordinal),
                        $"The body does not fit: {exception.Message}",
                        "Fix the value at the path: numbers are whole numbers, lists hold strings, and a field the format does not have is removed."
                    ),
                ]
            );
        }
    }

    public static Mistake Unknown(ResourceRef target, string field, string known) =>
        Make(
            MistakeCode.SaveUnknownField,
            target,
            field,
            $"'{field}' is not a field of this {ResourceKinds.Noun(target.Kind)}.",
            known.Length == 0
                ? $"Remove '{field}': this takes no fields."
                : $"Remove '{field}', or fix its spelling: the fields are {known}."
        );

    private static Mistake Make(MistakeCode code, ResourceRef target, string path, string message, string fix) =>
        new(code, ValidationStage.Save, target, path, message, fix, []);
}
