using System.Text.Json.Serialization;

namespace Suwagcms.Contracts;

/// <summary>What happened to a resource, or to a part of one, going from one version to another.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Difference>))]
public enum Difference
{
    Added,
    Removed,
    Changed,
}
