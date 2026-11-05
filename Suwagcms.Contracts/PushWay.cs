using System.Text.Json.Serialization;

namespace Suwagcms.Contracts;

/// <summary>How a push reaches the bot: the document is sent, a version the bot holds is made current without sending it, or nothing is needed because it is already current.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PushWay>))]
public enum PushWay
{
    Send,
    MakeCurrent,
    Nothing,
}
