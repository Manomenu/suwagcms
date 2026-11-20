using System.Text.Json.Serialization;

namespace Suwagcms.Contracts.Versions;

[JsonConverter(typeof(JsonStringEnumConverter<PushOutcome>))]
public enum PushOutcome
{
    Succeeded,
    Failed,
}
