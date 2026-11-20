using System.Text.Json;
using Suwagcms.Cli.Yaml;
using YamlDotNet.Serialization;

namespace Suwagcms.Cli.Procedures;

internal static class ProcedureYaml
{
    private static readonly ISerializer Serializer = new SerializerBuilder().WithQuotingNecessaryStrings().DisableAliases().Build();

    public static string From(JsonElement document) => Serializer.Serialize(FormatYaml.Plain(document, keepNulls: false)).TrimEnd();
}
