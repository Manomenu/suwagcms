using System.Text.Json.Serialization;

namespace Suwagcms.Server.Editing;

[JsonConverter(typeof(JsonStringEnumConverter<RefusalCode>))]
internal enum RefusalCode
{
    [JsonStringEnumMemberName("definition.invalid")]
    DefinitionInvalid,

    [JsonStringEnumMemberName("resource.stale")]
    ResourceStale,

    [JsonStringEnumMemberName("resource.missing")]
    ResourceMissing,

    [JsonStringEnumMemberName("bot.notFound")]
    BotNotFound,

    [JsonStringEnumMemberName("bot.invalid")]
    BotInvalid,

    [JsonStringEnumMemberName("bot.taken")]
    BotTaken,

    [JsonStringEnumMemberName("changes.invalid")]
    ChangesInvalid,

    [JsonStringEnumMemberName("version.missing")]
    VersionMissing,

    [JsonStringEnumMemberName("version.notPushed")]
    VersionNotPushed,

    [JsonStringEnumMemberName("history.kindUnknown")]
    HistoryKindUnknown,
}
