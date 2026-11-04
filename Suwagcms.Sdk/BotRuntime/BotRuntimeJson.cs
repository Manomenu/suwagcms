using System.Text.Json;
using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.BotRuntime;

internal static class BotRuntimeJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            NumberHandling = JsonNumberHandling.Strict,
            Converters = { new JsonStringEnumConverter() },
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
