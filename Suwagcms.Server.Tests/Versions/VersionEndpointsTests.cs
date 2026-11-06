using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Suwagcms.Contracts.Facts;
using Suwagcms.Contracts.Versions;
using Suwagcms.Sdk.BotRuntime;
using Suwagcms.Sdk.BotRuntime.Versions;
using Suwagcms.Sdk.Format;
using Suwagcms.Sdk.Format.Facts;
using Suwagcms.Sdk.Format.References;
using Suwagcms.Sdk.Format.Versions;
using Suwagcms.Sdk.Validation;
using Suwagcms.Server.Editing;
using Suwagcms.Server.Tests.Articles;
using Suwagcms.Server.Tests.Facts;
using Suwagcms.Server.Tests.Procedures;
using Suwagcms.Server.Versions;

namespace Suwagcms.Server.Tests.Versions;

public sealed class VersionEndpointsTests(TestDatabase database) : IDisposable
{
    private readonly TestApp app = new();

    public void Dispose() => app.Dispose();

    private async Task<VersionApi> NewBot()
    {
        _ = database.Pool;
        return await VersionApi.NewBotAsync(app.CreateClient());
    }

    private async Task<VersionApi> FullBot()
    {
        var api = await (await NewBot()).Ready();
        var articles = new ArticleApi(api.Client, api.Bot);
        Assert.True((await articles.KnowledgeBases.Add("offer", "Offer")).IsSuccessStatusCode);
        Assert.True((await articles.Add("priceList", ArticleApi.Body())).IsSuccessStatusCode);
        Assert.True((await api.Procedures.Add("orderValuation", ProcedureApi.Valuation)).IsSuccessStatusCode);
        return api;
    }

    private static GlobalFact Fact(string name, string description) =>
        new()
        {
            Name = name,
            Type = FactTypes.Text,
            Description = description,
            MaxLength = 5,
        };

    private static string Described(JsonElement document) =>
        string.Join(
            "|",
            document
                .EnumerateObject()
                .Where(member => member.Name is not ("bot" or "number" or "digest"))
                .Select(member => $"{member.Name}={member.Value.GetRawText()}")
        );

    [Fact]
    [Trait("criterion", "AC-knowledge-base-language-14")]
    public async Task A_version_document_holds_the_language_on_each_knowledge_base_and_none_on_its_articles()
    {
        var api = await (await NewBot()).Ready();
        var articles = new ArticleApi(api.Client, api.Bot);
        Assert.True((await articles.KnowledgeBases.AddWithLanguage("oferta", "Oferta", "pl")).IsSuccessStatusCode);
        Assert.True((await articles.KnowledgeBases.Add("offer", "Offer")).IsSuccessStatusCode);
        Assert.True((await articles.Add("cennik", ArticleApi.Body("oferta"))).IsSuccessStatusCode);

        var document = await api.DocumentOf((await api.Created()).Version.Number);

        Assert.Equal(
            ["oferta|pl", "offer|en"],
            document
                .GetProperty("knowledgeBases")
                .EnumerateArray()
                .Select(item => $"{item.GetProperty("id")}|{item.GetProperty("language")}")
                .Order(StringComparer.Ordinal)
        );
        Assert.False(document.GetProperty("articles")[0].TryGetProperty("language", out _));
    }

    [Fact]
    [Trait("criterion", "AC-version-create-1")]
    public async Task A_working_state_that_passes_the_Version_stage_becomes_the_next_version_and_is_listed()
    {
        var api = await (await NewBot()).Ready();

        var first = await api.Created();
        await api.Procedures.Put(
            "goodbye",
            """{"description":"Say goodbye","firstStep":"bye","steps":{"bye":{"say":"Bye"}}}""",
            "?ifRevision=0"
        );
        var second = await api.Created();

        Assert.Equal([1, 2], [first.Version.Number, second.Version.Number]);
        Assert.True(first.Written);
        Assert.Equal([2, 1], (await api.List()).Select(version => version.Number));
    }

    [Fact]
    [Trait("criterion", "AC-version-create-2")]
    public async Task An_unfinished_working_state_is_refused_with_every_mistake_and_uses_no_number()
    {
        var api = await NewBot();
        Assert.True(
            (
                await api.Procedures.Add(
                    "orderValuation",
                    """{"description":"d","firstStep":"a","steps":{"a":{"say":"Hi","next":"b"},"b":{"say":"Hi"},"c":{"say":"Alone"},"d":{"say":"Alone too"}}}"""
                )
            ).IsSuccessStatusCode
        );

        var refused = await api.Create();
        var codes = await VersionApi.CodesOf(refused);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal([MistakeCodes.BotConversationMissing, MistakeCodes.StepUnreachable, MistakeCodes.StepUnreachable], codes);
        Assert.Empty(await api.List());

        await api.Procedures.Put("orderValuation", ProcedureApi.Valuation);
        await api.Procedures.Add("conversation", ProcedureApi.Conversation);
        Assert.Equal(1, (await api.Created()).Version.Number);
    }

    [Fact]
    [Trait("criterion", "AC-version-create-3")]
    public async Task The_document_holds_formatVersion_the_bot_the_number_and_every_resource_as_it_was_saved()
    {
        var api = await FullBot();
        var created = await api.Created();

        var document = await api.DocumentOf(created.Version.Number);

        Assert.Equal(BotDefinition.CurrentFormatVersion, document.GetProperty("formatVersion").GetInt32());
        Assert.Equal(api.Bot, document.GetProperty("bot").GetString());
        Assert.Equal(1, document.GetProperty("number").GetInt32());
        Assert.Equal(
            ["customer.city", "order.floors", "order.kind", "system.flag"],
            document.GetProperty("facts").EnumerateArray().Select(item => item.GetProperty("name").GetString())
        );
        Assert.Equal(["offer"], document.GetProperty("knowledgeBases").EnumerateArray().Select(item => item.GetProperty("id").GetString()));
        Assert.Equal(["priceList"], document.GetProperty("articles").EnumerateArray().Select(item => item.GetProperty("id").GetString()));
        Assert.Equal(
            ["conversation", "orderValuation"],
            document.GetProperty("procedures").EnumerateArray().Select(item => item.GetProperty("id").GetString())
        );
        Assert.Equal("What a valuation costs.", document.GetProperty("articles")[0].GetProperty("content").GetString());
        Assert.Equal("ask", document.GetProperty("procedures")[1].GetProperty("firstStep").GetString());
    }

    [Fact]
    [Trait("criterion", "AC-version-create-4")]
    public async Task The_bot_runtime_of_the_SDK_accepts_a_created_document_at_the_Version_stage_and_loads_it()
    {
        var api = await FullBot();
        var created = await api.Created();
        var document = (await api.Detail(created.Version.Number)).Document;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSuwagcmsBot(api.Bot, new InMemoryVersionStore(), TimeSpan.FromHours(1));
        await using var bot = builder.Build();
        bot.MapSuwagcmsBot();
        await bot.StartAsync(TestContext.Current.CancellationToken);
        using var http = new HttpClient { BaseAddress = new Uri(bot.Urls.Single()) };
        using var content = new StringContent(document, System.Text.Encoding.UTF8, "application/json");

        using var pushed = await http.PutAsync(
            new Uri("/suwagcms/versions/1", UriKind.Relative),
            content,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, pushed.StatusCode);
        Assert.Empty(DefinitionValidator.ValidateJson(document, ValidationStage.Version));
        var loaded = await bot.Services.GetRequiredService<IBotDefinitions>().GetAsync(1, TestContext.Current.CancellationToken);
        Assert.Equal(4, loaded.Facts.Count);
        Assert.Equal(["conversation", "orderValuation"], loaded.Procedures.Select(procedure => procedure.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("criterion", "AC-version-create-5")]
    public async Task Edits_and_deletes_after_a_version_leave_its_document_byte_identical_and_its_digest_true()
    {
        var api = await FullBot();
        var before = await api.Created();
        var documentBefore = (await api.Detail(1)).Document;

        await api.Procedures.Facts.Put("customer.city", FactApi.Text("Changed", 20));
        await new ArticleApi(api.Client, api.Bot).Delete("priceList");
        await api.Procedures.Put("orderValuation", """{"description":"Changed","firstStep":"a","steps":{"a":{"say":"Hi"}}}""");
        var second = await api.Created();

        var after = await api.Detail(1);
        Assert.Equal(documentBefore, after.Document);
        Assert.Equal(before.Version.Sha256, after.Version.Sha256);
        Assert.Equal(after.Version.Sha256, VersionDigest.Of(after.Document));
        Assert.NotEqual(before.Version.Sha256, second.Version.Sha256);
        Assert.Contains("\"description\":\"Changed\"", (await api.Detail(2)).Document, StringComparison.Ordinal);
        Assert.Contains("\"description\":\"The city\"", after.Document, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("criterion", "AC-version-create-6")]
    public void The_routes_under_versions_are_reads_a_creation_and_a_push_that_changes_the_bot_never_a_version()
    {
        _ = database.Pool;
        var routes = app
            .Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Contains("/versions", StringComparison.Ordinal) == true)
            .SelectMany(endpoint =>
                (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["(any method)"]).Select(method =>
                    $"{method} {endpoint.RoutePattern.RawText}"
                )
            )
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                "GET /api/bots/{bot}/versions",
                "GET /api/bots/{bot}/versions/status",
                "GET /api/bots/{bot}/versions/{from:int}/diff/{to:int}",
                "GET /api/bots/{bot}/versions/{number:int}",
                "GET /api/bots/{bot}/versions/{number:int}/export",
                "GET /api/bots/{bot}/versions/{number:int}/push-plan",
                "POST /api/bots/{bot}/versions",
                "POST /api/bots/{bot}/versions/{number:int}/push",
            ],
            routes
        );
    }

    [Fact]
    [Trait("criterion", "AC-version-create-7")]
    public async Task The_same_state_saved_in_another_order_gives_the_same_document_apart_from_the_bot_and_the_digest()
    {
        var one = await (await NewBot()).Ready();
        var other = await (await NewBot()).Ready();
        await one.Procedures.Facts.Add("zeta.last", FactApi.Text("Last"));
        await one.Procedures.Facts.Add("alpha.first", FactApi.Text("First"));
        await other.Procedures.Facts.Add("alpha.first", FactApi.Text("First"));
        await other.Procedures.Facts.Add("zeta.last", FactApi.Text("Last"));
        var oneBases = new ArticleApi(one.Client, one.Bot).KnowledgeBases;
        var otherBases = new ArticleApi(other.Client, other.Bot).KnowledgeBases;
        await oneBases.Add("zulu", "Zulu");
        await oneBases.Add("alpha", "Alpha");
        await otherBases.Add("alpha", "Alpha");
        await otherBases.Add("zulu", "Zulu");

        await one.Created();
        await other.Created();

        var first = Described(await one.DocumentOf(1));
        Assert.Equal(first, Described(await other.DocumentOf(1)));
        Assert.Contains(
            "\"name\":\"alpha.first\"",
            first[..first.IndexOf("zeta.last", StringComparison.Ordinal)],
            StringComparison.Ordinal
        );
        Assert.Contains(
            "\"id\":\"alpha\"",
            first[first.IndexOf("knowledgeBases=", StringComparison.Ordinal)..first.IndexOf("zulu", StringComparison.Ordinal)],
            StringComparison.Ordinal
        );
    }

    [Fact]
    [Trait("criterion", "AC-version-create-8")]
    public async Task A_document_holds_only_the_definition_and_a_save_that_changes_nothing_of_it_counts_as_no_change()
    {
        var api = await FullBot();
        await api.Created();
        var document = await api.DocumentOf(1);

        Assert.Equal(
            ["formatVersion", "bot", "number", "digest", "facts", "knowledgeBases", "articles", "actions", "procedures"],
            document.EnumerateObject().Select(member => member.Name)
        );
        await api.Procedures.Put("orderValuation", ProcedureApi.Valuation);
        Assert.True((await api.Procedures.PutLayout("orderValuation", """{"positions":{"ask":{"x":10.5,"y":20}}}""")).IsSuccessStatusCode);
        var refused = await api.Create();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal([VersionCreator.Unchanged], await VersionApi.CodesOf(refused));
        Assert.Single(await api.List());
    }

    [Fact]
    [Trait("criterion", "AC-version-create-9")]
    public async Task Creating_again_when_nothing_changed_is_refused_with_a_mistake_saying_so_and_a_change_allows_it_again()
    {
        var api = await (await NewBot()).Ready();
        await api.Created();

        var refused = await api.Create();
        var mistake = Assert.Single(await FactApi.MistakesOf(refused));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
        Assert.Equal(VersionCreator.Unchanged, mistake.GetProperty("code").GetString());
        Assert.Contains("version 1", mistake.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Single(await api.List());
        await api.Procedures.Facts.Add("customer.extra", FactApi.Text("Extra"));
        Assert.Equal(2, (await api.Created()).Version.Number);
    }

    [Fact]
    [Trait("criterion", "AC-version-create-10")]
    public async Task Two_creations_at_the_same_moment_give_one_version_and_the_rest_are_refused()
    {
        var api = await (await NewBot()).Ready();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => api.Create()));

        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
        Assert.Equal(5, responses.Count(response => response.StatusCode == HttpStatusCode.UnprocessableEntity));
        foreach (var refused in responses.Where(response => response.StatusCode == HttpStatusCode.UnprocessableEntity))
        {
            Assert.Equal([VersionCreator.Unchanged], await VersionApi.CodesOf(refused));
        }
        Assert.Equal([1], (await api.List()).Select(version => version.Number));
    }

    [Fact]
    [Trait("criterion", "AC-version-create-10")]
    public async Task Concurrent_creations_of_changing_states_take_each_number_once_and_a_save_is_wholly_in_or_wholly_out()
    {
        var api = await (await NewBot()).Ready();
        var editor = new DefinitionEditor(database.Pool, TimeProvider.System);
        var creator = new VersionCreator(database.Pool, TimeProvider.System);
        var cancellation = TestContext.Current.CancellationToken;
        await editor.ApplyAsync(
            api.Bot,
            [
                new Put(new ResourceRef(ResourceKind.GlobalFact, "pair.a"), Fact("pair.a", "round 0"), 0),
                new Put(new ResourceRef(ResourceKind.GlobalFact, "pair.b"), Fact("pair.b", "round 0"), 0),
            ],
            false,
            cancellation
        );

        var saves = Enumerable
            .Range(1, 10)
            .Select(round =>
                editor.ApplyAsync(
                    api.Bot,
                    [
                        new Put(new ResourceRef(ResourceKind.GlobalFact, "pair.a"), Fact("pair.a", $"round {round}"), null),
                        new Put(new ResourceRef(ResourceKind.GlobalFact, "pair.b"), Fact("pair.b", $"round {round}"), null),
                    ],
                    false,
                    cancellation
                )
            );
        var creations = Enumerable.Range(0, 10).Select(_ => creator.CreateAsync(api.Bot, false, cancellation));
        await Task.WhenAll(saves);
        var results = await Task.WhenAll(creations);
        await creator.CreateAsync(api.Bot, false, cancellation);

        var listed = await api.List();
        Assert.All(results.Where(result => result.Created is null), result => Assert.IsType<EditResult.Refused>(result.Refusal));
        var numbers = listed.Select(version => version.Number).Order().ToList();
        Assert.Equal(Enumerable.Range(1, numbers.Count), numbers);
        Assert.NotEmpty(numbers);
        foreach (var number in numbers)
        {
            var facts = (await api.DocumentOf(number))
                .GetProperty("facts")
                .EnumerateArray()
                .ToDictionary(item => item.GetProperty("name").GetString() ?? "");
            Assert.Equal(facts["pair.a"].GetProperty("description").GetString(), facts["pair.b"].GetProperty("description").GetString());
        }
    }

    [Fact]
    [Trait("criterion", "AC-version-create-11")]
    public async Task Creating_a_version_sends_no_request_to_the_bot()
    {
        _ = database.Pool;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var address = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        var api = await (await VersionApi.NewBotAsync(app.CreateClient(), address)).Ready();

        await api.Created();
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.False(listener.Pending(), "a connection reached the address of the bot");
    }

    [Fact]
    [Trait("criterion", "AC-version-create-12")]
    public async Task A_dry_run_gives_the_same_mistakes_or_says_a_version_could_be_created_and_creates_nothing()
    {
        var failing = await NewBot();
        var real = await VersionApi.CodesOf(await failing.Create());
        var dry = await failing.Create(dryRun: true);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, dry.StatusCode);
        Assert.Equal(real, await VersionApi.CodesOf(dry));

        var api = await failing.Ready();
        var would = await api.Create(dryRun: true);
        Assert.Equal(HttpStatusCode.OK, would.StatusCode);
        var answer = await would.Content.ReadFromJsonAsync<VersionCreated>(TestContext.Current.CancellationToken);
        Assert.False(answer?.Written);
        Assert.Empty(await api.List());
        var created = await api.Created();
        Assert.Equal(answer?.Version.Number, created.Version.Number);
        Assert.Equal(answer?.Version.Sha256, created.Version.Sha256);
        var again = await api.Create(dryRun: true);
        Assert.Equal([VersionCreator.Unchanged], await VersionApi.CodesOf(again));
    }

    [Fact]
    [Trait("criterion", "AC-version-create-13")]
    public async Task The_list_shows_number_date_counts_and_digest_and_any_version_can_be_read()
    {
        var api = await FullBot();
        var created = await api.Created();

        var listed = Assert.Single(await api.List());
        var detail = await api.Detail(1);

        Assert.Equal((1, 4, 1, 1, 2), (listed.Number, listed.Facts, listed.KnowledgeBases, listed.Articles, listed.Procedures));
        Assert.Equal(created.Version.Sha256, listed.Sha256);
        Assert.InRange(listed.CreatedAt, TimeProvider.System.GetUtcNow().AddMinutes(-5), TimeProvider.System.GetUtcNow().AddMinutes(1));
        Assert.Equal(listed, detail.Version);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Get(7)).StatusCode);
        Assert.Equal([VersionEndpoints.Missing], await VersionApi.CodesOf(await api.Get(7)));
    }

    [Fact]
    [Trait("criterion", "AC-version-create-14")]
    public async Task Numbers_and_lists_are_per_bot()
    {
        var one = await (await NewBot()).Ready();
        var other = await (await NewBot()).Ready();
        await one.Created();
        await one.Procedures.Facts.Add("customer.extra", FactApi.Text("Extra"));
        await one.Created();

        var otherFirst = await other.Created();

        Assert.Equal(1, otherFirst.Version.Number);
        Assert.Equal([2, 1], (await one.List()).Select(version => version.Number));
        Assert.Equal([1], (await other.List()).Select(version => version.Number));
        Assert.Equal(HttpStatusCode.NotFound, (await other.Get(2)).StatusCode);
    }

    [Fact]
    [Trait("criterion", "AC-bot-profiles-13")]
    public async Task An_unknown_bot_is_a_404_for_every_route()
    {
        _ = database.Pool;
        var client = app.CreateClient();

        var responses = new[]
        {
            await client.GetAsync("/api/bots/nobody/versions", TestContext.Current.CancellationToken),
            await client.GetAsync("/api/bots/nobody/versions/1", TestContext.Current.CancellationToken),
            await client.PostAsync("/api/bots/nobody/versions", null, TestContext.Current.CancellationToken),
        };

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(["bot.notFound"], await VersionApi.CodesOf(response));
        }
    }
}
