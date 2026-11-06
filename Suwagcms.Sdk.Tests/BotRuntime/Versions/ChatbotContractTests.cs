using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Suwagcms.Sdk.BotRuntime;
using Suwagcms.Sdk.BotRuntime.Versions;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Versions;
using Suwagcms.Sdk.Validation;

namespace Suwagcms.Sdk.Tests.BotRuntime.Versions;

public sealed class ChatbotContractTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("criterion", "AC-chatbot-contract-7")]
    public async Task A_bot_with_nothing_says_so_and_the_first_push_makes_a_version_current_with_no_restart()
    {
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());
        Assert.Null(bot.Definitions.CurrentNumber);
        Assert.Null((await bot.StateAsync()).Current);

        var (status, _) = await bot.PutAsync(1, Documents.Make(1));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(1, bot.Definitions.CurrentNumber);
        Assert.Equal(1, (await bot.StateAsync()).Current);
    }

    [Fact]
    [Trait("criterion", "AC-chatbot-contract-8")]
    public async Task A_bot_restarted_over_the_same_store_has_what_it_had_with_no_push()
    {
        using var directory = new TempDirectory();
        await using (var before = await BotHost.StartAsync(new FileVersionStore(directory.Path)))
        {
            await before.PutAsync(1, Documents.Make(1));
            await before.PutAsync(2, Documents.Make(2, "second"));
        }

        await using var after = await BotHost.StartAsync(new FileVersionStore(directory.Path));

        Assert.Equal(2, after.Definitions.CurrentNumber);
        Assert.Equal([1, 2], (await after.StateAsync()).Held);
        Assert.NotNull(await after.Definitions.GetAsync(1, Token));
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-chatbot-contract-9")]
    public async Task A_conversations_number_gives_what_it_started_on_after_a_push_and_after_a_rollback(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        await bot.PutAsync(1, Documents.Make(1, "first"));
        var started = Assert.IsType<int>(bot.Definitions.CurrentNumber);
        var saw = FactName(await bot.Definitions.GetAsync(started, Token));

        await bot.PutAsync(2, Documents.Make(2, "second"));

        Assert.Equal(2, bot.Definitions.CurrentNumber);
        Assert.Equal(saw, FactName(await bot.Definitions.GetAsync(started, Token)));
        Assert.NotEqual(saw, FactName(await bot.Definitions.GetAsync(2, Token)));

        await bot.PutAsync(3, Documents.Make(3, "third"));
        var (status, _) = await bot.MakeCurrentAsync(2, VersionDigest.Of(Documents.Make(2, "second").ToJsonString()));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(2, bot.Definitions.CurrentNumber);
        Assert.Equal(saw, FactName(await bot.Definitions.GetAsync(started, Token)));
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-chatbot-contract-10")]
    public async Task A_number_reported_before_a_push_stays_held_until_a_report_no_longer_names_it(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        await bot.PutAsync(1, Documents.Make(1));
        await bot.Definitions.ReportInUseAsync([1], Token);

        await bot.PutAsync(2, Documents.Make(2));

        Assert.Equal([1, 2], (await bot.StateAsync()).Held);
        Assert.NotNull(await bot.Definitions.GetAsync(1, Token));

        await bot.Definitions.ReportInUseAsync([], Token);
        Assert.Equal([2], (await bot.StateAsync()).Held);
    }

    [Theory]
    [MemberData(nameof(Stores.Kinds), MemberType = typeof(Stores))]
    [Trait("criterion", "AC-chatbot-contract-10")]
    public async Task A_number_reported_before_a_rollback_stays_held(string kind)
    {
        using var directory = new TempDirectory();
        await using var bot = await BotHost.StartAsync(Stores.Create(kind, directory));
        await bot.PutAsync(1, Documents.Make(1));
        await bot.PutAsync(2, Documents.Make(2));
        await bot.Definitions.ReportInUseAsync([1, 2], Token);

        var (status, _) = await bot.MakeCurrentAsync(1, VersionDigest.Of(Documents.Make(1).ToJsonString()));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal([1, 2], (await bot.StateAsync()).Held);
    }

    [Fact]
    [Trait("criterion", "AC-chatbot-contract-11")]
    public async Task What_a_bot_cannot_take_is_refused_with_its_code_and_it_keeps_its_current_version()
    {
        var store = new FlakyStore(new InMemoryVersionStore());
        await using var bot = await BotHost.StartAsync(store);
        await bot.PutAsync(1, Documents.Make(1));

        var otherFormat = Documents.Make(2);
        otherFormat["formatVersion"] = 99;
        var wrongDigest = Documents.Make(2);
        wrongDigest["digest"] = "0";
        var refusals = new[]
        {
            (Document: Documents.Make(2, bot: "sales"), Code: MistakeCodes.VersionBot),
            (Document: otherFormat, Code: MistakeCodes.FormatVersion),
            (Document: wrongDigest, Code: MistakeCodes.VersionDigest),
        };
        foreach (var (document, code) in refusals)
        {
            var (status, body) = await bot.PutAsync(2, document);

            Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
            Assert.Contains(body["mistakes"]?.AsArray() ?? [], mistake => mistake?["code"]?.GetValue<string>() == code);
            Assert.Equal(1, bot.Definitions.CurrentNumber);
        }

        store.FailWrites = true;
        var (failed, _) = await bot.PutAsync(2, Documents.Make(2));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed);
        Assert.Equal(1, bot.Definitions.CurrentNumber);
    }

    [Fact]
    [Trait("criterion", "AC-chatbot-contract-12")]
    public async Task A_filter_on_what_the_mapping_returns_closes_every_route()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSuwagcmsBot("rzeczoznawca", new InMemoryVersionStore());
        await using var app = builder.Build();
        app.MapSuwagcmsBot().AddEndpointFilter((_, _) => ValueTask.FromResult<object?>(Results.StatusCode(StatusCodes.Status403Forbidden)));
        await app.StartAsync(Token);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        using var empty = new StringContent("{}");

        var answers = new[]
        {
            await client.GetAsync(new Uri("/suwagcms/state", UriKind.Relative), Token),
            await client.GetAsync(new Uri("/suwagcms/declaration", UriKind.Relative), Token),
            await client.PutAsync(new Uri("/suwagcms/versions/1", UriKind.Relative), empty, Token),
            await client.PutAsync(new Uri("/suwagcms/current", UriKind.Relative), empty, Token),
            await client.PutAsync(new Uri("/suwagcms/availability", UriKind.Relative), empty, Token),
            await client.PostAsync(new Uri("/suwagcms/actions/test", UriKind.Relative), empty, Token),
        };

        Assert.All(answers, answer => Assert.Equal(HttpStatusCode.Forbidden, answer.StatusCode));
        await app.StopAsync(Token);
    }

    [Fact]
    [Trait("criterion", "AC-chatbot-contract-13")]
    public async Task A_version_with_a_field_this_sdk_does_not_know_loads_and_its_digest_holds()
    {
        await using var bot = await BotHost.StartAsync(new InMemoryVersionStore());
        var document = Documents.Make(1);
        document["aFieldOfTomorrow"] = "unknown";
        document["procedures"]?[0]?.AsObject().Add("anotherFieldOfTomorrow", 1);

        var (status, _) = await bot.PutAsync(1, Documents.Sign(document));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(1, bot.Definitions.CurrentNumber);
        Assert.Single((await bot.Definitions.GetAsync(1, Token)).Procedures);
    }

    private static string FactName(BotDefinition definition) => definition.Facts.Single().Name;
}
