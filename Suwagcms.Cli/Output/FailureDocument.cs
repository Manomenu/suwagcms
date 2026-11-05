using System.Text.Json;

namespace Suwagcms.Cli.Output;

internal sealed record FailureDocument(
    string Error,
    string? Server = null,
    IReadOnlyList<JsonElement>? Mistakes = null,
    string? Usage = null
);
