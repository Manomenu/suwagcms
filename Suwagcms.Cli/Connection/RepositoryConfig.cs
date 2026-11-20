using System.Text.Json;
using System.Text.Json.Nodes;
using Suwagcms.Cli.Yaml;
using YamlDotNet.Core;

namespace Suwagcms.Cli.Connection;

internal sealed record RepositoryConfig(string? SuwagcmsUrl, string? BotId)
{
    public const string DirOption = "--dir";
    public const string DefaultFolder = "suwagcms";
    public const string FileName = "config.yaml";
    public const string AddressKey = "suwagcmsUrl";
    public const string BotKey = "botId";

    private static readonly string[] Keys = [AddressKey, BotKey];

    public static (RepositoryConfig? Config, string? Problem) Find(string? dir, string workingDirectory)
    {
        if (dir is null && !Path.IsPathRooted(workingDirectory))
        {
            return (null, null);
        }
        var path = Path.Combine(workingDirectory, dir ?? DefaultFolder, FileName);
        if (!File.Exists(path))
        {
            return dir is null
                ? (null, null)
                : (null, $"{DirOption} {dir} holds no {FileName}: name the folder that has one, or leave the option out.");
        }
        return Read(File.ReadAllText(path), path);
    }

    private static (RepositoryConfig? Config, string? Problem) Read(string text, string path)
    {
        JsonNode? root;
        try
        {
            root = FormatYaml.Parse(text, new HashSet<string>());
        }
        catch (Exception error) when (error is YamlException or JsonException)
        {
            return (null, $"{path} cannot be read: {error.Message} Fix it, so that it holds {string.Join(" and ", Keys)}.");
        }
        if (root is null)
        {
            return (new RepositoryConfig(null, null), null);
        }
        if (root is not JsonObject values)
        {
            return (null, $"{path} is no mapping: write it as {AddressKey}: <url> and {BotKey}: <id>.");
        }
        var unknown = values.Select(value => value.Key).Except(Keys).Order(StringComparer.Ordinal).ToList();
        return unknown.Count > 0
            ? (null, $"{path} has the unknown key {string.Join(", ", unknown)}: the keys are {string.Join(" and ", Keys)}.")
            : (new RepositoryConfig(values[AddressKey]?.ToString(), values[BotKey]?.ToString()), null);
    }
}
