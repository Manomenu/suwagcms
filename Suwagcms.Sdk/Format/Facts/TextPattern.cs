using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format.Facts;

public sealed record TextPattern
{
    public string Regex { get; init; } = "";

    public string Means { get; init; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Example { get; init; }

    public string TellCustomer { get; init; } = TellCustomers.Upfront;
}
