using System.Text.Json.Serialization;

namespace Suwagcms.Contracts.Versions;

[JsonConverter(typeof(JsonStringEnumConverter<Difference>))]
public enum Difference
{
    Added,
    Removed,
    Changed,
}
