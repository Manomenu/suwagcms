using System.Text.Json.Serialization;

namespace Suwagcms.Contracts.Versions;

[JsonConverter(typeof(JsonStringEnumConverter<LineChange>))]
public enum LineChange
{
    Kept,
    Removed,
    Added,
    Folded,
}
