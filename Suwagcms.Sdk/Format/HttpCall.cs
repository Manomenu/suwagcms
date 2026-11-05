using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format;

// Why: every field has a default so that a bad action lists all its mistakes instead of stopping at the first; a secret stands in a header only, as {secret:NAME}.
public sealed record HttpCall
{
    public const int DefaultTimeoutSeconds = 10;

    public string Method { get; init; } = "";

    // Why: on the wire it is the url, and it holds {name} placeholders, so it is a template and never a System.Uri.
    [JsonPropertyName("url")]
    public string Address { get; init; } = "";

    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    public int? TimeoutSeconds { get; init; }
}
