using System.Text.Json.Serialization;

namespace Suwagcms.Contracts;

/// <summary>What a line of a long text is in a comparison: around a change, gone, new, or standing for unchanged lines left out.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LineChange>))]
public enum LineChange
{
    Kept,
    Removed,
    Added,
    Folded,
}
