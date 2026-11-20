using System.Text.Json.Nodes;
using Suwagcms.Sdk.Format.Versions;

namespace Suwagcms.Sdk.Tests.BotRuntime;

internal static class Documents
{
    public static JsonObject Make(int number, string label = "a", string bot = "rzeczoznawca")
    {
        var document = new JsonObject
        {
            ["formatVersion"] = 5,
            ["bot"] = bot,
            ["number"] = number,
            ["facts"] = new JsonArray(Fact(label)),
            ["procedures"] = new JsonArray(Conversation()),
        };
        return Sign(document);
    }

    public static JsonObject WithArticles(int number, params JsonObject[] articles)
    {
        var document = Make(number);
        document["knowledgeBases"] = new JsonArray(
            new JsonObject
            {
                ["id"] = "faq",
                ["title"] = "Questions",
                ["language"] = "pl",
                ["embedding"] = "legalText",
                ["results"] = 5,
            }
        );
        document["articles"] = new JsonArray([.. articles]);
        return Sign(document);
    }

    public static JsonObject Article(string id, string content = "Text.") =>
        new()
        {
            ["id"] = id,
            ["knowledgeBase"] = "faq",
            ["title"] = $"Title of {id}",
            ["description"] = $"What {id} answers",
            ["content"] = content,
            ["llmGuide"] = $"Guide of {id}",
        };

    public static JsonObject Fact(string name) =>
        new()
        {
            ["name"] = name,
            ["type"] = "text",
            ["description"] = "A fact for the tests",
            ["maxLength"] = 10,
        };

    public static JsonObject Conversation() =>
        new()
        {
            ["id"] = "conversation",
            ["description"] = "The base of every conversation",
            ["firstStep"] = "hello",
            ["steps"] = new JsonObject { ["hello"] = new JsonObject { ["say"] = "Hello" } },
        };

    public static JsonObject Sign(JsonObject document)
    {
        document["digest"] = VersionDigest.Of(document.ToJsonString());
        return document;
    }
}
