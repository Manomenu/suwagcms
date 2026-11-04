using System.Text.Json.Nodes;

namespace Suwagcms.Sdk.Tests.BotRuntime;

internal static class JsonNodeExtensions
{
    public static JsonNode Member(this JsonNode node, string name) =>
        node[name] ?? throw new InvalidOperationException($"No member '{name}'.");

    public static JsonNode Item(this JsonNode node, int index) => node[index] ?? throw new InvalidOperationException($"No item {index}.");

    public static string Text(this JsonNode node) => node.GetValue<string>();

    public static IEnumerable<JsonNode> Items(this JsonNode node) =>
        node.AsArray().Select(item => item ?? throw new InvalidOperationException("A null item."));
}
