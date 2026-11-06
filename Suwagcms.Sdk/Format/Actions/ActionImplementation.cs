using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format.Actions;

// Why: the format writes an implementation as the word code or as { http: ... }, and one record holds whichever it is.
[JsonConverter(typeof(ActionImplementationConverter))]
public sealed record ActionImplementation
{
    public const string CodeWord = "code";

    public bool IsCode { get; init; }

    public HttpCall? Http { get; init; }
}
