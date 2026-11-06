using System.Text.Json;
using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format;

internal static class DefinitionFormat
{
    public const int Version = 2;

    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            // Why: a bot on an older SDK must load a version that has a newer optional field.
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            RespectNullableAnnotations = true,
            PropertyNameCaseInsensitive = false,
            NumberHandling = JsonNumberHandling.Strict,
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
