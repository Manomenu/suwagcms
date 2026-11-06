using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format.Facts;

// Why: Regex and Means default to empty so that a missing one reaches the rules, which name it; the words are the author's, as code cannot say what a regular expression means.
public sealed record TextPattern
{
    public string Regex { get; init; } = "";

    public string Means { get; init; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Example { get; init; }

    public string TellCustomer { get; init; } = TellCustomers.Upfront;
}
