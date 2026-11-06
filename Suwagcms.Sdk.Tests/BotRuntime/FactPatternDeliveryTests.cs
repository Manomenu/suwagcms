using System.Text.Json.Nodes;
using Suwagcms.Sdk.Format;

namespace Suwagcms.Sdk.Tests.BotRuntime;

public sealed class FactPatternDeliveryTests
{
    private static JsonObject Pattern() =>
        new()
        {
            ["regex"] = "[A-Z]{2}",
            ["means"] = "two capitals",
            ["example"] = "AB",
            ["tellCustomer"] = "afterRefusal",
        };

    [Fact]
    [Trait("criterion", "AC-fact-pattern-1")]
    public async Task A_version_holding_a_global_and_a_local_pattern_gives_both_to_the_bots_code()
    {
        await using var bot = await BotHost.StartAsync(new Suwagcms.Sdk.BotRuntime.InMemoryVersionStore());
        var document = Documents.Make(1);
        var global = Documents.Fact("order.code");
        global["pattern"] = Pattern();
        document["facts"] = new JsonArray(global);
        var local = Documents.Conversation();
        local["localFacts"] = new JsonObject
        {
            ["code"] = new JsonObject
            {
                ["type"] = "text",
                ["description"] = "A local fact",
                ["maxLength"] = 10,
                ["pattern"] = Pattern(),
            },
        };
        document["procedures"] = new JsonArray(local);
        await bot.PutAsync(1, Documents.Sign(document));

        var loaded = await bot.Definitions.GetAsync(1, TestContext.Current.CancellationToken);

        var expected = new TextPattern
        {
            Regex = "[A-Z]{2}",
            Means = "two capitals",
            Example = "AB",
            TellCustomer = TellCustomers.AfterRefusal,
        };
        Assert.Equal(expected, Assert.Single(loaded.Facts).Pattern);
        Assert.Equal(expected, Assert.Single(loaded.Procedures).Locals["code"].Pattern);
    }
}
