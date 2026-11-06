using System.Text.Json;
using Suwagcms.Cli.Yaml;
using YamlDotNet.Serialization;

namespace Suwagcms.Cli.Procedures;

internal static class ProcedureYaml
{
    private static readonly ISerializer Serializer = new SerializerBuilder().WithQuotingNecessaryStrings().DisableAliases().Build();

    // Why: the server sends a field a procedure does not have as null, and read back from YAML a null block would be an empty one, so nulls are left out.
    public static string From(JsonElement document) => Serializer.Serialize(FormatYaml.Plain(document, keepNulls: false)).TrimEnd();
}
