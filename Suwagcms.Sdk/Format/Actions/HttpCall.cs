using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format.Actions;

public sealed record HttpCall
{
    public const int DefaultTimeoutSeconds = 10;

    public string Method { get; init; } = "";

    [JsonPropertyName("url")]
    public string Address { get; init; } = "";

    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    public int? TimeoutSeconds { get; init; }
}
