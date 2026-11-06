using System.Text.Json.Serialization;

namespace Suwagcms.Contracts.Versions;

/// <summary>How an attempt to push a version ended.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PushOutcome>))]
public enum PushOutcome
{
    Succeeded,
    Failed,
}
