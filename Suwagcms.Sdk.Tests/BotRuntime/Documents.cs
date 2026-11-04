using System.Text.Json.Nodes;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Tests.BotRuntime;

internal static class Documents
{
    public static JsonObject Make(int number, string label = "a", string bot = "rzeczoznawca")
    {
        var document = new JsonObject
        {
            ["formatVersion"] = 1,
            ["bot"] = bot,
            ["number"] = number,
            ["facts"] = new JsonArray(Fact(label)),
        };
        return Sign(document);
    }

    public static JsonObject Fact(string name) =>
        new()
        {
            ["name"] = name,
            ["type"] = "text",
            ["description"] = "A fact for the tests",
            ["maxLength"] = 10,
        };

    public static JsonObject Sign(JsonObject document)
    {
        document["digest"] = VersionDigest.Of(document.ToJsonString());
        return document;
    }
}
