using System.Text.Json.Serialization;

namespace Suwagcms.Sdk.Format.Actions;

[JsonConverter(typeof(ActionImplementationConverter))]
public sealed record ActionImplementation
{
    public const string CodeWord = "code";

    public bool IsCode { get; init; }

    public HttpCall? Http { get; init; }
}
