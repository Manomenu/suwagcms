using System.Net;
using System.Text.Json.Nodes;
using Suwagcms.Sdk.BotRuntime.Versions;
using Suwagcms.Sdk.Tests.BotRuntime.Versions;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.BotRuntime;

public sealed class RoutesTests
{
    [Fact]
    [Trait("criterion", "AC-bot-runtime-sdk-2")]
    public async Task Before_any_push_the_bot_has_no_current_version()
    {
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());

        var state = await bot.StateAsync();

        Assert.Null(bot.Definitions.CurrentNumber);
        Assert.Null(state.Current);
        Assert.Empty(state.Held);
        Assert.Equal("rzeczoznawca", state.Bot);
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-bot-runtime-sdk-3")]
    public async Task A_version_that_fails_validation_is_refused_with_every_mistake_and_the_state_stays(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        await bot.PutAsync(1, Documents.Make(1));
        var bad = Documents.Make(2);
        bad["facts"] = new JsonArray(Documents.Fact("x"), Documents.Fact("x"), Documents.Fact("y"), Documents.Fact("y"));
        Documents.Sign(bad);

        var (status, body) = await bot.PutAsync(2, bad);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        var mistakes = body.Member("mistakes").Items();
        Assert.Equal(2, mistakes.Count(mistake => mistake.Member("code").Text() == MistakeCodes.DuplicateId));
        var state = await bot.StateAsync();
        Assert.Equal(1, state.Current);
        Assert.Equal([1], state.Held);
        Assert.Equal(1, bot.Definitions.CurrentNumber);
        Assert.Equal("a", (await bot.Definitions.GetAsync(1, TestContext.Current.CancellationToken)).Facts.Single().Name);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => bot.Definitions.GetAsync(2, TestContext.Current.CancellationToken));
    }

    public static TheoryData<string, string> WrongEnvelopes =>
        new()
        {
            { "bot", MistakeCodes.VersionBot },
            { "formatVersion", MistakeCodes.FormatVersion },
            { "digest", MistakeCodes.VersionDigest },
            { "number", MistakeCodes.VersionNumber },
        };

    [Theory]
    [MemberData(nameof(WrongEnvelopes))]
    [Trait("criterion", "AC-bot-runtime-sdk-4")]
    public async Task A_document_of_another_bot_or_format_or_with_a_wrong_digest_is_refused_naming_the_reason(string broken, string code)
    {
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());
        await bot.PutAsync(1, Documents.Make(1));
        var document = Documents.Make(2);
        switch (broken)
        {
            case "bot":
                document["bot"] = "another-bot";
                Documents.Sign(document);
                break;
            case "formatVersion":
                document["formatVersion"] = 7;
                Documents.Sign(document);
                break;
            case "digest":
                document["facts"] = new JsonArray(Documents.Fact("changed after signing"));
                break;
            default:
                document["number"] = 3;
                Documents.Sign(document);
                break;
        }

        var (status, body) = await bot.PutAsync(2, document);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        var refusal = body.Member("mistakes").Items().Single(mistake => mistake.Member("code").Text() == code);
        Assert.False(string.IsNullOrWhiteSpace(refusal.Member("message").Text()));
        var state = await bot.StateAsync();
        Assert.Equal(1, state.Current);
        Assert.Equal([1], state.Held);
    }

    [Fact]
    [Trait("criterion", "AC-bot-runtime-sdk-4")]
    public async Task The_refusal_of_an_unknown_format_names_the_version_the_sdk_reads()
    {
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());
        var document = Documents.Make(1);
        document["formatVersion"] = 7;

        var (_, body) = await bot.PutAsync(1, document);

        Assert.Contains("reads 5", body.Member("mistakes").Item(0).Member("message").Text(), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-bot-runtime-sdk-13")]
    public async Task When_the_store_fails_during_a_write_the_route_answers_with_the_reason_and_the_state_stays()
    {
        var store = new FlakyStore(new InMemoryVersionStore());
        await using var bot = await BotHost.StartAsync(store);
        await bot.PutAsync(1, Documents.Make(1));
        store.FailWrites = true;

        var (status, body) = await bot.PutAsync(2, Documents.Make(2));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Contains("the store is down (write)", body.Member("detail").Text(), StringComparison.Ordinal);
        store.FailWrites = false;
        var state = await bot.StateAsync();
        Assert.Equal(1, state.Current);
        Assert.Equal([1], state.Held);
        Assert.Equal(1, bot.Definitions.CurrentNumber);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => bot.Definitions.GetAsync(2, TestContext.Current.CancellationToken));
    }

    [Fact]
    [Trait("criterion", "AC-bot-runtime-sdk-14")]
    public async Task When_the_store_cannot_be_read_the_instance_keeps_what_it_has_and_the_state_route_says_so()
    {
        var store = new FlakyStore(new InMemoryVersionStore());
        await using var bot = await BotHost.StartAsync(store);
        await bot.PutAsync(1, Documents.Make(1));
        await bot.PutAsync(2, Documents.Make(2));
        store.FailReads = true;

        var state = await bot.StateAsync();

        Assert.Contains("the store is down (read)", state.StoreError, StringComparison.Ordinal);
        Assert.Equal(2, state.Current);
        Assert.Equal(2, bot.Definitions.CurrentNumber);
        Assert.Equal("a", (await bot.Definitions.GetAsync(1, TestContext.Current.CancellationToken)).Facts.Single().Name);
        store.FailReads = false;
        Assert.Null((await bot.StateAsync()).StoreError);
    }
}
