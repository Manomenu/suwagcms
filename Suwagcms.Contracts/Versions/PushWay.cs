using System.Text.Json.Serialization;

namespace Suwagcms.Contracts.Versions;

[JsonConverter(typeof(JsonStringEnumConverter<PushWay>))]
public enum PushWay
{
    Send,
    MakeCurrent,
    Nothing,
}
